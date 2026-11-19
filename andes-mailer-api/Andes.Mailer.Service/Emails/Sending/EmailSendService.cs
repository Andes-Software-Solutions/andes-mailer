using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails.Exceptions;
using Andes.Mailer.Service.Exceptions;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Service.Emails.Sending;

/// <summary>
/// Sends, schedules, and retries a team's emails. Every operation is scoped to one team, which the caller has already been checked against.
/// </summary>
/// <remarks>
/// Sending only queues the email: it gets one delivery per recipient, and <see cref="EmailDispatchProcessor"/> sends them in the background.
/// </remarks>
public interface IEmailSendService
{
    /// <summary>
    /// Sends one of a team's drafts now, or schedules it for later.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="id">The email's id.</param>
    /// <param name="senderId">The Entra object id of the signed-in user.</param>
    /// <param name="action">When to send it and the row version the caller last read.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The email, now <see cref="EmailStatuses.Sending"/> or <see cref="EmailStatuses.Scheduled"/>.</returns>
    /// <exception cref="NotFoundException">The team has no active email with the id.</exception>
    /// <exception cref="EmailNotEditableException">The email is no longer a draft.</exception>
    /// <exception cref="EmailNotSendableException">The email has no subject, no recipient, or an attachment whose upload failed.</exception>
    /// <exception cref="EmailNotReadyException">One of the email's attachments is still uploading.</exception>
    /// <exception cref="ConflictException">The email changed since the caller read it.</exception>
    Task<EmailDto> SendAsync(Guid teamId, Guid id, Guid senderId, SendEmailActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Returns one of a team's scheduled emails to a draft, as long as no recipient has been picked up yet.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="id">The email's id.</param>
    /// <param name="action">The row version the caller last read.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The email, a draft again.</returns>
    /// <exception cref="NotFoundException">The team has no active email with the id.</exception>
    /// <exception cref="EmailNotScheduledException">The email is not scheduled.</exception>
    /// <exception cref="EmailAlreadySendingException">The dispatcher has started sending the email.</exception>
    /// <exception cref="ConflictException">The email changed since the caller read it.</exception>
    Task<EmailDto> UnscheduleAsync(Guid teamId, Guid id, EmailVersionActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Queues the failed recipients of one of a team's emails again, each with a fresh set of attempts. Recipients already sent are never touched.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="id">The email's id.</param>
    /// <param name="action">The row version the caller last read.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The email, <see cref="EmailStatuses.Sending"/> again.</returns>
    /// <exception cref="NotFoundException">The team has no active email with the id.</exception>
    /// <exception cref="EmailNotRetryableException">The email is not <see cref="EmailStatuses.PartiallySent"/> or <see cref="EmailStatuses.Failed"/>.</exception>
    /// <exception cref="ConflictException">The email changed since the caller read it.</exception>
    Task<EmailDto> RetryFailedAsync(Guid teamId, Guid id, EmailVersionActionDto action, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the deliveries of one of a team's emails, one per recipient.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="id">The email's id.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The deliveries, ordered by header (To, Cc, Bcc), then by the order the recipients were entered; empty for a draft.</returns>
    /// <exception cref="NotFoundException">The team has no active email with the id.</exception>
    Task<IReadOnlyList<EmailDeliveryDto>> ListDeliveriesAsync(Guid teamId, Guid id, CancellationToken cancellationToken);
}

/// <summary>
/// Queues a team's emails for the dispatcher in the SQL store.
/// </summary>
/// <param name="ctx">The store.</param>
/// <param name="signal">Wakes the dispatcher for an email sent now.</param>
/// <param name="time">The clock that stamps the send.</param>
public sealed class EmailSendService(AndesMailerDbContext ctx, EmailDispatchSignal signal, TimeProvider time) : IEmailSendService
{
    // One message for "missing" and "another team's", so the API never confirms that another team's email exists.
    private const string NotFoundMessage = "The email was not found.";

    private readonly AndesMailerDbContext _ctx = ctx;
    private readonly EmailDispatchSignal _signal = signal;
    private readonly TimeProvider _time = time;

    /// <inheritdoc/>
    public async Task<EmailDto> SendAsync(Guid teamId, Guid id, Guid senderId, SendEmailActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var email = await _ctx.Emails
            .Include(e => e.Recipients)
            .Include(e => e.Attachments)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id && e.TeamId == teamId, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);
        if (email.StatusId != EmailStatuses.Draft)
        {
            throw new EmailNotEditableException();
        }

        if (string.IsNullOrWhiteSpace(email.Subject))
        {
            throw new EmailNotSendableException("Add a subject before sending the email.");
        }

        if (email.Recipients.Count == 0)
        {
            throw new EmailNotSendableException("Add at least one recipient before sending the email.");
        }

        if (email.Attachments.Any(a => a.StatusId is AttachmentStatuses.Pending or AttachmentStatuses.Uploading))
        {
            throw new EmailNotReadyException();
        }

        if (email.Attachments.Any(a => a.StatusId == AttachmentStatuses.Failed))
        {
            throw new EmailNotSendableException("Remove the attachments whose upload failed before sending the email.");
        }

        // The version the client last read, so the UPDATE matches no row if anyone changed the email since.
        _ctx.Entry(email).Property(e => e.Version).OriginalValue = Convert.FromBase64String(action.Version);
        var now = _time.GetUtcNow();
        email.StatusId = action.ScheduledFor is null ? EmailStatuses.Sending : EmailStatuses.Scheduled;
        email.ScheduledFor = action.ScheduledFor;
        email.SentById = senderId;
        email.DateSendRequested = now;
        email.DateCompleted = null;
        foreach (var recipient in email.Recipients)
        {
            email.Deliveries.Add(new EmailDelivery
            {
                EmailId = email.Id,
                RecipientId = recipient.Id,
                StatusId = DeliveryStatuses.Pending,
                NextAttemptAt = action.ScheduledFor ?? now,
            });
        }

        await SaveAsync(cancellationToken);
        if (action.ScheduledFor is null)
        {
            _signal.Notify();
        }

        return EmailMapper.MapToEmailDto(email);
    }

    /// <inheritdoc/>
    public async Task<EmailDto> UnscheduleAsync(Guid teamId, Guid id, EmailVersionActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        // The whole operation inside the strategy, so a retried transaction starts again from a fresh read.
        var strategy = _ctx.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(
            async ct =>
            {
                _ctx.ChangeTracker.Clear();
                var email = await _ctx.Emails
                    .Include(e => e.Recipients)
                    .Include(e => e.Attachments)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(e => e.Id == id && e.TeamId == teamId, ct)
                    ?? throw new NotFoundException(NotFoundMessage);
                if (email.StatusId != EmailStatuses.Scheduled)
                {
                    throw new EmailNotScheduledException();
                }

                await using var transaction = await _ctx.Database.BeginTransactionAsync(ct);

                // The email first: once it is a draft the dispatcher no longer promotes it, and a promotion that won the race moved the
                // version, so this save conflicts instead.
                _ctx.Entry(email).Property(e => e.Version).OriginalValue = Convert.FromBase64String(action.Version);
                email.StatusId = EmailStatuses.Draft;
                email.ScheduledFor = null;
                email.SentById = null;
                email.DateSendRequested = null;
                await SaveAsync(ct);

                // Deleted rather than deactivated: the recipients they point at can change again now the email is a draft.
                var total = await _ctx.EmailDeliveries.CountAsync(d => d.EmailId == id, ct);
                var removed = await _ctx.EmailDeliveries
                    .Where(d => d.EmailId == id && d.StatusId == DeliveryStatuses.Pending && d.AttemptCount == 0 && d.GraphMessageId == null)
                    .ExecuteDeleteAsync(ct);
                if (removed != total)
                {
                    throw new EmailAlreadySendingException();
                }

                await transaction.CommitAsync(ct);

                return EmailMapper.MapToEmailDto(email);
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<EmailDto> RetryFailedAsync(Guid teamId, Guid id, EmailVersionActionDto action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        var email = await _ctx.Emails
            .Include(e => e.Recipients)
            .Include(e => e.Attachments)
            .Include(e => e.Deliveries)
            .AsSplitQuery()
            .FirstOrDefaultAsync(e => e.Id == id && e.TeamId == teamId, cancellationToken)
            ?? throw new NotFoundException(NotFoundMessage);
        if (email.StatusId is not (EmailStatuses.PartiallySent or EmailStatuses.Failed))
        {
            throw new EmailNotRetryableException();
        }

        _ctx.Entry(email).Property(e => e.Version).OriginalValue = Convert.FromBase64String(action.Version);
        var now = _time.GetUtcNow();

        // The draft id stays: if an earlier send went through before its result was saved, the dispatcher finds the draft gone and does
        // not send again.
        foreach (var delivery in email.Deliveries.Where(d => d.StatusId == DeliveryStatuses.Failed))
        {
            delivery.StatusId = DeliveryStatuses.Pending;
            delivery.AttemptCount = 0;
            delivery.NextAttemptAt = now;
            delivery.LeaseExpiresAt = null;
            delivery.LastError = null;
        }

        email.StatusId = EmailStatuses.Sending;
        email.DateCompleted = null;
        await SaveAsync(cancellationToken);
        _signal.Notify();

        return EmailMapper.MapToEmailDto(email);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EmailDeliveryDto>> ListDeliveriesAsync(Guid teamId, Guid id, CancellationToken cancellationToken)
    {
        if (!await _ctx.Emails.AnyAsync(e => e.Id == id && e.TeamId == teamId, cancellationToken))
        {
            throw new NotFoundException(NotFoundMessage);
        }

        return await _ctx.EmailDeliveries
            .AsNoTracking()
            .Where(d => d.EmailId == id)
            .OrderBy(d => d.Recipient!.RecipientTypeId)
            .ThenBy(d => d.Recipient!.Position)
            .Select(d => new EmailDeliveryDto(
                d.Id,
                d.Recipient!.Address,
                d.Recipient.DisplayName,
                d.StatusId,
                d.AttemptCount,
                d.StatusId == DeliveryStatuses.Pending || d.StatusId == DeliveryStatuses.Sending ? d.NextAttemptAt : null,
                d.DateSent,
                d.LastError))
            .ToListAsync(cancellationToken);
    }

    #region Private methods

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _ctx.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The email was changed by another request. Read it again and retry.");
        }
    }

    #endregion
}
