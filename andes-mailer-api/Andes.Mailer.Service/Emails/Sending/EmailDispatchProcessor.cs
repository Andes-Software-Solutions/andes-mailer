using System.Diagnostics;
using System.Linq.Expressions;

using Andes.Mailer.Common.Constants;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails.Sending.Exceptions;
using Andes.Mailer.Service.Emails.Sending.Models;
using Andes.Mailer.Service.Observability;
using Andes.Mailer.Service.Options;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Andes.Mailer.Service.Emails.Sending;

/// <summary>
/// Sends emails to their recipients in the background, on the CRON schedule in <see cref="EmailSendingOptions.Schedule"/> and whenever
/// <see cref="EmailDispatchSignal"/> wakes it. The <c>[Core].[EmailDelivery]</c> rows are its queue.
/// </summary>
/// <remarks>
/// <para>
/// Each run moves scheduled emails that have come due to <see cref="EmailStatuses.Sending"/>, sends their due deliveries, and settles the
/// emails whose deliveries have all finished. A delivery is claimed with a conditional update before it is sent, so two runs, on one
/// instance or several, never send it twice; a claim whose lease runs out, because the process died mid-send, is picked up again and
/// counts as an attempt.
/// </para>
/// <para>
/// A send fault is retried with a growing delay up to <see cref="EmailSendingOptions.MaxAttempts"/>; a rejection that another attempt
/// cannot fix fails the delivery at once, and throttling defers it without counting an attempt. A sent delivery is never claimed again.
/// </para>
/// </remarks>
/// <param name="scopes">Creates a DI scope, and so a store context, per run and per delivery.</param>
/// <param name="signal">Wakes the dispatcher before its next scheduled run.</param>
/// <param name="time">The clock that schedules runs and stamps deliveries.</param>
/// <param name="options">The sending settings.</param>
/// <param name="logger">The logger.</param>
public sealed partial class EmailDispatchProcessor(
    IServiceScopeFactory scopes,
    EmailDispatchSignal signal,
    TimeProvider time,
    IOptions<EmailSendingOptions> options,
    ILogger<EmailDispatchProcessor> logger) : BackgroundService
{
    private const string DeactivatedReason = "The email or its team was deactivated before it could be sent.";
    private const string InterruptedReason = "Sending was interrupted too many times.";

    private static readonly TimeSpan _maxWait = TimeSpan.FromDays(1);

    private readonly IServiceScopeFactory _scopes = scopes;
    private readonly EmailDispatchSignal _signal = signal;
    private readonly TimeProvider _time = time;
    private readonly EmailSendingOptions _options = options.Value;
    private readonly ILogger<EmailDispatchProcessor> _logger = logger;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            LogDisabled(_logger);
            return;
        }

        var schedule = EmailSendingOptionsValidator.ParseSchedule(_options.Schedule);
        while (!stoppingToken.IsCancellationRequested)
        {
            // A run at startup too, so deliveries the last shutdown left behind do not wait for the schedule.
            await RunSafelyAsync(stoppingToken);

            var now = _time.GetUtcNow();
            var next = schedule.GetNextOccurrence(now, TimeZoneInfo.Utc);
            if (next is null)
            {
                LogNoNextRun(_logger, _options.Schedule);
                await _signal.WaitAsync(stoppingToken);
                continue;
            }

            // Capped because WaitAsync rejects a timeout of about 49.7 days or more; waking early only runs a harmless extra dispatch.
            var untilNext = next.Value - now;
            var wait = untilNext < _maxWait ? untilNext : _maxWait;

            // The read is cancelled once the scheduled time wins, so no abandoned reader is left to swallow the next wake-up.
            using var wake = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            try
            {
                await _signal.WaitAsync(wake.Token).WaitAsync(wait, _time, stoppingToken);
            }
            catch (TimeoutException)
            {
                // The scheduled time came first.
            }
            finally
            {
                await wake.CancelAsync();
            }
        }
    }

    /// <summary>
    /// Runs one dispatch: promotes scheduled emails that are due, sends due deliveries, and settles finished emails.
    /// </summary>
    /// <param name="cancellationToken">A token that stops the run.</param>
    /// <returns>A task that completes when the run ends.</returns>
    internal async Task DispatchOnceAsync(CancellationToken cancellationToken)
    {
        var now = _time.GetUtcNow();
        List<DueDelivery> due;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AndesMailerDbContext>();
            await FailExhaustedAsync(ctx, now, cancellationToken);
            await PromoteScheduledAsync(ctx, now, cancellationToken);
            due = await FindDueAsync(ctx, now, cancellationToken);
        }

        await Parallel.ForEachAsync(
            due.GroupBy(d => d.MailboxId),
            new ParallelOptions { MaxDegreeOfParallelism = _options.MaxConcurrency, CancellationToken = cancellationToken },
            async (mailbox, token) =>
            {
                // One after another per mailbox, so the mailbox's send rate stays under the mail server's limit.
                foreach (var delivery in mailbox.Take(_options.MaxPerMailboxPerRun))
                {
                    if (await DeliverSafelyAsync(delivery.Id, token))
                    {
                        break;
                    }
                }
            });

        await using (var scope = _scopes.CreateAsyncScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AndesMailerDbContext>();
            await CompleteFinishedAsync(ctx, _time.GetUtcNow(), cancellationToken);
        }
    }

    #region Private methods

    private async Task RunSafelyAsync(CancellationToken cancellationToken)
    {
        // A root activity, so the run's store and Graph calls, its deliveries, and its logs share one operation in telemetry.
        using var activity = AndesMailerActivitySource.Instance.StartActivity(TelemetryNames.EmailDispatchActivity);
        try
        {
            await DispatchOnceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down: claimed deliveries are released, and the rest wait for the next start.
        }
#pragma warning disable CA1031 // The run boundary: a store fault must not stop the dispatcher; the next run tries again.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            LogRunFailed(_logger, ex);
        }
    }

    // A claim that ran out with no claim left: the process died mid-send each time, so the delivery is not tried again.
    private async Task FailExhaustedAsync(AndesMailerDbContext ctx, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var maxAttempts = _options.MaxAttempts;
        var failed = await ctx.EmailDeliveries
            .Where(d => d.StatusId == DeliveryStatuses.Sending
                && d.LeaseExpiresAt < now
                && d.AttemptCount >= (d.GraphMessageId != null ? maxAttempts + 1 : maxAttempts))
            .ExecuteUpdateAsync(
                s => s.SetProperty(d => d.StatusId, DeliveryStatuses.Failed)
                    .SetProperty(d => d.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(d => d.LastError, InterruptedReason)
                    .SetProperty(d => d.DateModified, now),
                cancellationToken);
        if (failed > 0)
        {
            LogExhausted(_logger, failed);
        }
    }

    private async Task PromoteScheduledAsync(AndesMailerDbContext ctx, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var promoted = await ctx.Emails
            .Where(e => e.StatusId == EmailStatuses.Scheduled && e.ScheduledFor <= now)
            .ExecuteUpdateAsync(
                s => s.SetProperty(e => e.StatusId, EmailStatuses.Sending).SetProperty(e => e.DateModified, now),
                cancellationToken);
        if (promoted > 0)
        {
            LogPromoted(_logger, promoted);
        }
    }

    private Task<List<DueDelivery>> FindDueAsync(AndesMailerDbContext ctx, DateTimeOffset now, CancellationToken cancellationToken) =>
        ctx.EmailDeliveries
            .AsNoTracking()
            .Where(IsDue(now))
            .Where(d => d.Email!.StatusId == EmailStatuses.Sending && d.Email.Team!.DateDeactivated == null)
            .OrderBy(d => d.NextAttemptAt)
            .ThenBy(d => d.Id)
            .Take(_options.BatchSize)
            .Select(d => new DueDelivery(d.Id, d.Email!.Team!.ServiceAccountId))
            .ToListAsync(cancellationToken);

    // Pending and due, or claimed by a run whose lease ran out with claims to spare. A delivery with a draft gets one claim beyond its
    // attempts: its last send may have gone out, so that claim only checks the draft before the delivery is settled.
    private Expression<Func<EmailDelivery, bool>> IsDue(DateTimeOffset now)
    {
        var maxAttempts = _options.MaxAttempts;

        return d => (d.StatusId == DeliveryStatuses.Pending && d.NextAttemptAt <= now)
            || (d.StatusId == DeliveryStatuses.Sending
                && d.LeaseExpiresAt < now
                && d.AttemptCount < (d.GraphMessageId != null ? maxAttempts + 1 : maxAttempts));
    }

    // Returns true when the mailbox is throttled, so the run stops sending from it.
    private async Task<bool> DeliverSafelyAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        using var activity = AndesMailerActivitySource.Instance.StartActivity(TelemetryNames.EmailDeliveryActivity);
        activity?.SetTag(TelemetryNames.DeliveryIdTag, deliveryId);
        try
        {
            return await DeliverAsync(deliveryId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // The delivery boundary: a store fault leaves the claim to run out, and a later run retries the delivery.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            activity?.SetStatus(ActivityStatusCode.Error, ex.GetType().Name);
            LogDeliveryFaulted(_logger, ex, deliveryId);

            return false;
        }
    }

    private async Task<bool> DeliverAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AndesMailerDbContext>();
        var mailbox = scope.ServiceProvider.GetRequiredService<IMailboxClient>();

        var now = _time.GetUtcNow();
        var lease = now + _options.LeaseDuration;
        var claimed = await ctx.EmailDeliveries
            .Where(d => d.Id == deliveryId)
            .Where(IsDue(now))
            .ExecuteUpdateAsync(
                s => s.SetProperty(d => d.StatusId, DeliveryStatuses.Sending)
                    .SetProperty(d => d.LeaseExpiresAt, lease)
                    .SetProperty(d => d.AttemptCount, d => d.AttemptCount + 1)
                    .SetProperty(d => d.DateModified, now),
                cancellationToken);
        if (claimed != 1)
        {
            // Another run claimed it first, or it stopped being due.
            return false;
        }

        var job = await LoadJobAsync(ctx, deliveryId, cancellationToken);
        if (job is null)
        {
            await SettleAsync(ctx, deliveryId, lease, s => Fail(s, DeactivatedReason), CancellationToken.None);
            return false;
        }

        if (job.AttemptCount > _options.MaxAttempts && job.GraphMessageId is null)
        {
            await SettleAsync(ctx, deliveryId, lease, s => Fail(s, InterruptedReason), CancellationToken.None);
            return false;
        }

        var messageId = job.GraphMessageId;
        var draftMailboxId = job.DraftMailboxId ?? job.MailboxId;
        var sendCalled = false;
        var accepted = false;
        try
        {
            if (messageId is not null)
            {
                if (!await mailbox.DraftExistsAsync(draftMailboxId, messageId, cancellationToken))
                {
                    // An earlier attempt's send went through before its result could be saved.
                    await SettleAsync(ctx, deliveryId, lease, Sent, CancellationToken.None);
                    LogSent(_logger, deliveryId, job.AttemptCount);
                    return false;
                }

                if (job.AttemptCount > _options.MaxAttempts)
                {
                    // The check-only claim after an interrupted last attempt: the draft is still unsent, so that attempt failed.
                    await SettleAsync(ctx, deliveryId, lease, s => Fail(s, InterruptedReason), CancellationToken.None);
                    LogInterrupted(_logger, deliveryId);
                    return false;
                }

                if (draftMailboxId != job.MailboxId)
                {
                    // The team changed mailbox since the draft was made. The draft is unsent, so it is dropped and the message goes from the new one.
                    if (!await SettleAsync(ctx, deliveryId, lease, ClearDraft, cancellationToken))
                    {
                        LogLeaseLost(_logger, deliveryId);
                        return false;
                    }

                    await DeleteDraftBestEffortAsync(mailbox, draftMailboxId, deliveryId, messageId);
                    messageId = null;
                }
            }

            if (messageId is null)
            {
                var created = await mailbox.CreateDraftAsync(job.MailboxId, job.Message, cancellationToken);
                var createdIn = job.MailboxId;

                // Saved before the send, with its mailbox, so a later attempt can tell whether this send went through.
                if (!await SettleAsync(
                    ctx,
                    deliveryId,
                    lease,
                    s => s.SetProperty(d => d.GraphMessageId, created).SetProperty(d => d.DraftMailboxId, createdIn),
                    cancellationToken))
                {
                    // The run that took over never sees this draft, so it is removed; that run makes its own.
                    LogLeaseLost(_logger, deliveryId);
                    await DeleteDraftBestEffortAsync(mailbox, createdIn, deliveryId, created);
                    return false;
                }

                messageId = created;
                draftMailboxId = createdIn;
            }

            sendCalled = true;
            await mailbox.SendDraftAsync(draftMailboxId, messageId, cancellationToken);
            accepted = true;
            await SettleAsync(ctx, deliveryId, lease, Sent, CancellationToken.None);
            LogSent(_logger, deliveryId, job.AttemptCount);

            return false;
        }
#pragma warning disable CA1031 // The message went out; only recording it failed, and the next attempt finds the draft gone and records it.
        catch (Exception ex) when (accepted)
#pragma warning restore CA1031
        {
            LogSentNotRecorded(_logger, ex, deliveryId);

            return false;
        }
        catch (MailboxException ex) when (ex.Kind == MailboxFailureKinds.Throttled)
        {
            var delay = ex.RetryAfter ?? _options.RetryBaseDelay;
            LogThrottled(_logger, deliveryId, delay);
            await SettleAsync(
                ctx,
                deliveryId,
                lease,
                s => s.SetProperty(d => d.StatusId, DeliveryStatuses.Pending)
                    .SetProperty(d => d.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(d => d.NextAttemptAt, now + delay)
                    .SetProperty(d => d.AttemptCount, d => d.AttemptCount - 1)
                    .SetProperty(d => d.LastError, ex.Message),
                CancellationToken.None);

            return true;
        }
        catch (MailboxException ex) when (ex.Kind == MailboxFailureKinds.Permanent)
        {
            // Once the send was called, a rejection can be the echo of a retried request that went through, such as a 404 for a draft
            // already sent, so the draft decides. Before that, a failure leaves nothing known to be unsent.
            var unsent = sendCalled ? await IsDraftUnsentAsync(mailbox, draftMailboxId, messageId!, deliveryId) : null;
            if (unsent == false)
            {
                await SettleAsync(ctx, deliveryId, lease, Sent, CancellationToken.None);
                LogSent(_logger, deliveryId, job.AttemptCount);
                return false;
            }

            // Only a draft known to be unsent is discarded, so a manual retry starts from a new one; any other keeps its id for the next check.
            LogRejected(_logger, ex, deliveryId);
            var discard = unsent == true;
            var settled = await SettleAsync(
                ctx,
                deliveryId,
                lease,
                s =>
                {
                    Fail(s, ex.Message);
                    if (discard)
                    {
                        ClearDraft(s);
                    }
                },
                CancellationToken.None);
            if (discard && settled)
            {
                await DeleteDraftBestEffortAsync(mailbox, draftMailboxId, deliveryId, messageId);
            }

            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down: the attempt is given back, so a restart does not count it against the delivery.
            await SettleAsync(
                ctx,
                deliveryId,
                lease,
                s => s.SetProperty(d => d.StatusId, DeliveryStatuses.Pending)
                    .SetProperty(d => d.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(d => d.AttemptCount, d => d.AttemptCount - 1),
                CancellationToken.None);
            throw;
        }
#pragma warning disable CA1031 // A transient mail or store fault: retried with a growing delay, then settled as Failed.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // The draft is kept either way: a timed-out send may still have gone out, and the next attempt, or a manual retry, checks.
            var reason = ex is MailboxException ? ex.Message : "The message could not be sent.";
            if (job.AttemptCount >= _options.MaxAttempts)
            {
                if (sendCalled && await IsDraftUnsentAsync(mailbox, draftMailboxId, messageId!, deliveryId) == false)
                {
                    await SettleAsync(ctx, deliveryId, lease, Sent, CancellationToken.None);
                    LogSent(_logger, deliveryId, job.AttemptCount);
                    return false;
                }

                LogFailed(_logger, ex, deliveryId, job.AttemptCount);
                await SettleAsync(ctx, deliveryId, lease, s => Fail(s, reason), CancellationToken.None);

                return false;
            }

            var delay = RetryDelay(job.AttemptCount);
            LogRetrying(_logger, ex, deliveryId, job.AttemptCount, delay);
            await SettleAsync(
                ctx,
                deliveryId,
                lease,
                s => s.SetProperty(d => d.StatusId, DeliveryStatuses.Pending)
                    .SetProperty(d => d.LeaseExpiresAt, (DateTimeOffset?)null)
                    .SetProperty(d => d.NextAttemptAt, now + delay)
                    .SetProperty(d => d.LastError, reason),
                CancellationToken.None);

            return false;
        }
    }

    // True while the draft waits unsent, false once it is gone because it was sent, null when the mailbox cannot say.
    private async Task<bool?> IsDraftUnsentAsync(IMailboxClient mailbox, Guid mailboxId, string messageId, Guid deliveryId)
    {
        try
        {
            return await mailbox.DraftExistsAsync(mailboxId, messageId, CancellationToken.None);
        }
#pragma warning disable CA1031 // Not knowing is an answer here: the delivery keeps its draft id, and a later check decides.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogDraftCheckFailed(_logger, ex, deliveryId);

            return null;
        }
    }

    private static Task<DeliveryJob?> LoadJobAsync(AndesMailerDbContext ctx, Guid deliveryId, CancellationToken cancellationToken) =>
        ctx.EmailDeliveries
            .AsNoTracking()
            .Where(d => d.Id == deliveryId && d.Email!.Team!.DateDeactivated == null)
            .Select(d => new DeliveryJob(
                d.AttemptCount,
                d.GraphMessageId,
                d.DraftMailboxId,
                d.Email!.Team!.ServiceAccountId,
                new OutgoingMessage(
                    d.Email.Subject,
                    d.Email.BodyHtml,
                    d.Recipient!.Address,
                    d.Recipient.DisplayName,
                    d.Email.Attachments
                        .Where(a => a.StatusId == AttachmentStatuses.Uploaded)
                        .OrderBy(a => a.DateCreated)
                        .ThenBy(a => a.Id)
                        .Select(a => new OutgoingAttachment(a.FileName, a.ContentType, a.SizeBytes, a.BlobName))
                        .ToList())))
            .FirstOrDefaultAsync(cancellationToken);

    // Writes only while this run still holds the claim, so a run whose lease ran out never overwrites the run that took over.
    private async Task<bool> SettleAsync(
        AndesMailerDbContext ctx,
        Guid deliveryId,
        DateTimeOffset lease,
        Action<UpdateSettersBuilder<EmailDelivery>> set,
        CancellationToken cancellationToken)
    {
        var modified = _time.GetUtcNow();
        var updated = await ctx.EmailDeliveries
            .IgnoreQueryFilters()
            .Where(d => d.Id == deliveryId && d.StatusId == DeliveryStatuses.Sending && d.LeaseExpiresAt == lease)
            .ExecuteUpdateAsync(
                s =>
                {
                    set(s);
                    s.SetProperty(d => d.DateModified, modified);
                },
                cancellationToken);

        return updated == 1;
    }

    private void Sent(UpdateSettersBuilder<EmailDelivery> s) =>
        s.SetProperty(d => d.StatusId, DeliveryStatuses.Sent)
            .SetProperty(d => d.LeaseExpiresAt, (DateTimeOffset?)null)
            .SetProperty(d => d.DateSent, _time.GetUtcNow())
            .SetProperty(d => d.LastError, (string?)null);

    private static void ClearDraft(UpdateSettersBuilder<EmailDelivery> s) =>
        s.SetProperty(d => d.GraphMessageId, (string?)null).SetProperty(d => d.DraftMailboxId, (Guid?)null);

    private static void Fail(UpdateSettersBuilder<EmailDelivery> s, string reason) =>
        s.SetProperty(d => d.StatusId, DeliveryStatuses.Failed)
            .SetProperty(d => d.LeaseExpiresAt, (DateTimeOffset?)null)
            .SetProperty(d => d.LastError, reason);

    private TimeSpan RetryDelay(int attempt)
    {
        var delay = _options.RetryBaseDelay * Math.Pow(2, attempt - 1);

        return delay < _options.RetryMaxDelay ? delay : _options.RetryMaxDelay;
    }

    // Best effort: a draft that is left behind only clutters the mailbox's Drafts folder.
    private async Task DeleteDraftBestEffortAsync(IMailboxClient mailbox, Guid mailboxId, Guid deliveryId, string? messageId)
    {
        if (messageId is null)
        {
            return;
        }

        try
        {
            await mailbox.DeleteDraftAsync(mailboxId, messageId, CancellationToken.None);
        }
#pragma warning disable CA1031 // The delivery is already settled; a leftover draft is not worth failing anything for.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogDraftCleanupFailed(_logger, ex, deliveryId);
        }
    }

    // An email whose deliveries have all finished: Sent if every one went out, Failed if none did, PartiallySent otherwise.
    private async Task CompleteFinishedAsync(AndesMailerDbContext ctx, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var finished = await ctx.Emails
            .AsNoTracking()
            .Where(e => e.StatusId == EmailStatuses.Sending
                && !e.Deliveries.Any(d => d.StatusId == DeliveryStatuses.Pending || d.StatusId == DeliveryStatuses.Sending))
            .Select(e => new { e.Id, Sent = e.Deliveries.Count(d => d.StatusId == DeliveryStatuses.Sent), Total = e.Deliveries.Count })
            .ToListAsync(cancellationToken);
        foreach (var email in finished)
        {
            var status = email.Sent == email.Total ? EmailStatuses.Sent
                : email.Sent == 0 ? EmailStatuses.Failed
                : EmailStatuses.PartiallySent;

            // The same conditions again, so a retry that re-queued a delivery since the read above keeps the email sending.
            var updated = await ctx.Emails
                .Where(e => e.Id == email.Id
                    && e.StatusId == EmailStatuses.Sending
                    && !e.Deliveries.Any(d => d.StatusId == DeliveryStatuses.Pending || d.StatusId == DeliveryStatuses.Sending))
                .ExecuteUpdateAsync(
                    s => s.SetProperty(e => e.StatusId, status)
                        .SetProperty(e => e.DateCompleted, now)
                        .SetProperty(e => e.DateModified, now),
                    cancellationToken);
            if (updated == 1)
            {
                LogCompleted(_logger, email.Id, status, email.Sent, email.Total);
            }
        }
    }

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Email sending is disabled on this instance")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The email sending schedule '{Schedule}' never runs again; only immediate sends wake the dispatcher")]
    private static partial void LogNoNextRun(ILogger logger, string schedule);

    [LoggerMessage(Level = LogLevel.Error, Message = "An email dispatch run failed; the next run tries again")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed {Count} email deliveries interrupted on their last attempt")]
    private static partial void LogExhausted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Started sending {Count} scheduled emails")]
    private static partial void LogPromoted(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sent delivery {DeliveryId} on attempt {Attempt}")]
    private static partial void LogSent(ILogger logger, Guid deliveryId, int attempt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Delivery {DeliveryId} was sent but could not be recorded; the next attempt records it without sending again")]
    private static partial void LogSentNotRecorded(ILogger logger, Exception exception, Guid deliveryId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Delivery {DeliveryId} was interrupted on its last attempt and its draft is still unsent; it is not tried again")]
    private static partial void LogInterrupted(ILogger logger, Guid deliveryId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not check the draft of delivery {DeliveryId}; it keeps its draft id for the next check")]
    private static partial void LogDraftCheckFailed(ILogger logger, Exception exception, Guid deliveryId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Lost the claim on delivery {DeliveryId} before sending it; the run that took over sends it")]
    private static partial void LogLeaseLost(ILogger logger, Guid deliveryId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The mailbox of delivery {DeliveryId} is throttled; trying again in {Delay}")]
    private static partial void LogThrottled(ILogger logger, Guid deliveryId, TimeSpan delay);

    [LoggerMessage(Level = LogLevel.Error, Message = "The mail server rejected delivery {DeliveryId}; it is not tried again")]
    private static partial void LogRejected(ILogger logger, Exception exception, Guid deliveryId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Delivery {DeliveryId} failed on its last attempt ({Attempt})")]
    private static partial void LogFailed(ILogger logger, Exception exception, Guid deliveryId, int attempt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Delivery {DeliveryId} failed on attempt {Attempt}; retrying in {Delay}")]
    private static partial void LogRetrying(ILogger logger, Exception exception, Guid deliveryId, int attempt, TimeSpan delay);

    [LoggerMessage(Level = LogLevel.Error, Message = "Delivery {DeliveryId} faulted; its claim runs out and a later run retries it")]
    private static partial void LogDeliveryFaulted(ILogger logger, Exception exception, Guid deliveryId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete the draft of delivery {DeliveryId}; it is left in the mailbox's drafts")]
    private static partial void LogDraftCleanupFailed(ILogger logger, Exception exception, Guid deliveryId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Email {EmailId} finished as {Status}: {Sent} of {Total} recipients sent")]
    private static partial void LogCompleted(ILogger logger, Guid emailId, EmailStatuses status, int sent, int total);

    #endregion
}
