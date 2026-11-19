using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails.Exceptions;
using Andes.Mailer.Service.Emails.Sending;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Unit.Test.Service.Emails.Sending;

public sealed class EmailSendServiceTests : IDisposable
{
    private readonly MailerDbFixture _fixture = new();
    private readonly EmailDispatchSignal _signal = new();

    [Fact]
    public async Task SendAsync_Now_QueuesOneDeliveryPerRecipientAndWakesTheDispatcher()
    {
        var (teamId, senderId, email) = await SeedDraftAsync("ada@contoso.com", "bob@contoso.com");
        await using var ctx = _fixture.CreateContext();

        var sent = await CreateService(ctx).SendAsync(teamId, email.Id, senderId, new SendEmailActionDto(Version(email), null), TestContext.Current.CancellationToken);

        Assert.Equal(EmailStatuses.Sending, sent.Status);
        Assert.Equal(senderId, sent.SentById);
        Assert.Equal(_fixture.Time.GetUtcNow(), sent.DateSendRequested);
        Assert.Null(sent.ScheduledFor);
        Assert.Equal(2, sent.Deliveries.Pending);
        var deliveries = await ReadDeliveriesAsync(email.Id);
        Assert.Equal(2, deliveries.Count);
        Assert.All(deliveries, d =>
        {
            Assert.Equal(DeliveryStatuses.Pending, d.StatusId);
            Assert.Equal(0, d.AttemptCount);
            Assert.Equal(_fixture.Time.GetUtcNow(), d.NextAttemptAt);
        });
        Assert.True(await WasSignalledAsync());
    }

    [Fact]
    public async Task SendAsync_Scheduled_QueuesDeliveriesForTheScheduledTimeWithoutWaking()
    {
        var (teamId, senderId, email) = await SeedDraftAsync("ada@contoso.com");
        var at = _fixture.Time.GetUtcNow().AddHours(3);
        await using var ctx = _fixture.CreateContext();

        var scheduled = await CreateService(ctx).SendAsync(teamId, email.Id, senderId, new SendEmailActionDto(Version(email), at), TestContext.Current.CancellationToken);

        Assert.Equal(EmailStatuses.Scheduled, scheduled.Status);
        Assert.Equal(at, scheduled.ScheduledFor);
        var delivery = Assert.Single(await ReadDeliveriesAsync(email.Id));
        Assert.Equal(at, delivery.NextAttemptAt);
        Assert.False(await WasSignalledAsync());
    }

    [Theory]
    [InlineData(EmailStatuses.Sending)]
    [InlineData(EmailStatuses.Scheduled)]
    [InlineData(EmailStatuses.Sent)]
    public async Task SendAsync_NotADraft_ThrowsNotEditable(EmailStatuses status)
    {
        var (teamId, senderId, email) = await SeedDraftAsync(status, "Report", "ada@contoso.com");
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<EmailNotEditableException>(() =>
            CreateService(ctx).SendAsync(teamId, email.Id, senderId, new SendEmailActionDto(Version(email), null), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_NoRecipients_ThrowsNotSendable()
    {
        var (teamId, senderId, email) = await SeedDraftAsync();
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<EmailNotSendableException>(() =>
            CreateService(ctx).SendAsync(teamId, email.Id, senderId, new SendEmailActionDto(Version(email), null), TestContext.Current.CancellationToken));
        Assert.Empty(await ReadDeliveriesAsync(email.Id));
    }

    [Fact]
    public async Task SendAsync_EmptySubject_ThrowsNotSendable()
    {
        var (teamId, senderId, email) = await SeedDraftAsync(EmailStatuses.Draft, " ", "ada@contoso.com");
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<EmailNotSendableException>(() =>
            CreateService(ctx).SendAsync(teamId, email.Id, senderId, new SendEmailActionDto(Version(email), null), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(AttachmentStatuses.Pending)]
    [InlineData(AttachmentStatuses.Uploading)]
    public async Task SendAsync_AttachmentStillUploading_ThrowsNotReady(AttachmentStatuses status)
    {
        var (teamId, senderId, email) = await SeedDraftAsync("ada@contoso.com");
        await AddAttachmentAsync(email.Id, status);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<EmailNotReadyException>(() =>
            CreateService(ctx).SendAsync(teamId, email.Id, senderId, new SendEmailActionDto(Version(email), null), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_AttachmentFailed_ThrowsNotSendable()
    {
        var (teamId, senderId, email) = await SeedDraftAsync("ada@contoso.com");
        await AddAttachmentAsync(email.Id, AttachmentStatuses.Failed);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<EmailNotSendableException>(() =>
            CreateService(ctx).SendAsync(teamId, email.Id, senderId, new SendEmailActionDto(Version(email), null), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SendAsync_StaleVersion_ThrowsConflictAndQueuesNothing()
    {
        var (teamId, senderId, email) = await SeedDraftAsync("ada@contoso.com");
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(ctx).SendAsync(teamId, email.Id, senderId, new SendEmailActionDto(Convert.ToBase64String(new byte[8]), null), TestContext.Current.CancellationToken));
        Assert.Empty(await ReadDeliveriesAsync(email.Id));
    }

    [Fact]
    public async Task SendAsync_AnotherTeamsEmail_ThrowsNotFound()
    {
        var (_, senderId, email) = await SeedDraftAsync("ada@contoso.com");
        var otherTeam = await _fixture.AddTeamAsync();
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService(ctx).SendAsync(otherTeam.Id, email.Id, senderId, new SendEmailActionDto(Version(email), null), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnscheduleAsync_Scheduled_ReturnsADraftAndRemovesTheDeliveries()
    {
        var (teamId, email) = await SeedScheduledAsync();
        await using var ctx = _fixture.CreateContext();

        var draft = await CreateService(ctx).UnscheduleAsync(teamId, email.Id, new EmailVersionActionDto(Version(email)), TestContext.Current.CancellationToken);

        Assert.Equal(EmailStatuses.Draft, draft.Status);
        Assert.Null(draft.ScheduledFor);
        Assert.Null(draft.SentById);
        Assert.Empty(await ReadDeliveriesAsync(email.Id));
    }

    [Fact]
    public async Task UnscheduleAsync_RecipientAlreadyPickedUp_ThrowsAlreadySendingAndKeepsTheSchedule()
    {
        var (teamId, email) = await SeedScheduledAsync();
        await using (var seed = _fixture.CreateContext())
        {
            await seed.EmailDeliveries.Where(d => d.EmailId == email.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.AttemptCount, 1), TestContext.Current.CancellationToken);
        }

        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<EmailAlreadySendingException>(() =>
            CreateService(ctx).UnscheduleAsync(teamId, email.Id, new EmailVersionActionDto(Version(email)), TestContext.Current.CancellationToken));

        await using var read = _fixture.CreateContext();
        var stored = await read.Emails.SingleAsync(e => e.Id == email.Id, TestContext.Current.CancellationToken);
        Assert.Equal(EmailStatuses.Scheduled, stored.StatusId);
        Assert.NotNull(stored.ScheduledFor);
        Assert.Single(await ReadDeliveriesAsync(email.Id));
    }

    [Fact]
    public async Task UnscheduleAsync_NotScheduled_ThrowsNotScheduled()
    {
        var (teamId, _, email) = await SeedDraftAsync("ada@contoso.com");
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<EmailNotScheduledException>(() =>
            CreateService(ctx).UnscheduleAsync(teamId, email.Id, new EmailVersionActionDto(Version(email)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RetryFailedAsync_PartiallySent_RequeuesOnlyTheFailedRecipients()
    {
        var (teamId, email) = await SeedFinishedAsync(EmailStatuses.PartiallySent, DeliveryStatuses.Sent, DeliveryStatuses.Failed);
        await using var ctx = _fixture.CreateContext();

        var retried = await CreateService(ctx).RetryFailedAsync(teamId, email.Id, new EmailVersionActionDto(Version(email)), TestContext.Current.CancellationToken);

        Assert.Equal(EmailStatuses.Sending, retried.Status);
        Assert.Null(retried.DateCompleted);
        Assert.Equal(new(1, 1, 0), retried.Deliveries);
        var deliveries = await ReadDeliveriesAsync(email.Id);
        var sent = Assert.Single(deliveries, d => d.StatusId == DeliveryStatuses.Sent);
        Assert.Equal(1, sent.AttemptCount);
        var requeued = Assert.Single(deliveries, d => d.StatusId == DeliveryStatuses.Pending);
        Assert.Equal(0, requeued.AttemptCount);
        Assert.Null(requeued.LastError);
        Assert.Equal(_fixture.Time.GetUtcNow(), requeued.NextAttemptAt);
        Assert.True(await WasSignalledAsync());
    }

    [Theory]
    [InlineData(EmailStatuses.Draft)]
    [InlineData(EmailStatuses.Sending)]
    [InlineData(EmailStatuses.Sent)]
    public async Task RetryFailedAsync_NotFinishedWithFailures_ThrowsNotRetryable(EmailStatuses status)
    {
        var (teamId, email) = await SeedFinishedAsync(status, DeliveryStatuses.Sent);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<EmailNotRetryableException>(() =>
            CreateService(ctx).RetryFailedAsync(teamId, email.Id, new EmailVersionActionDto(Version(email)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListDeliveriesAsync_SentEmail_ListsEachRecipientInEntryOrder()
    {
        var (teamId, email) = await SeedFinishedAsync(EmailStatuses.PartiallySent, DeliveryStatuses.Sent, DeliveryStatuses.Failed);
        await using var ctx = _fixture.CreateContext();

        var deliveries = await CreateService(ctx).ListDeliveriesAsync(teamId, email.Id, TestContext.Current.CancellationToken);

        Assert.Equal(["r0@contoso.com", "r1@contoso.com"], deliveries.Select(d => d.Address));
        Assert.Equal([DeliveryStatuses.Sent, DeliveryStatuses.Failed], deliveries.Select(d => d.Status));
        Assert.All(deliveries, d => Assert.Null(d.NextAttemptAt));
    }

    [Fact]
    public async Task ListDeliveriesAsync_AnotherTeamsEmail_ThrowsNotFound()
    {
        var (_, email) = await SeedFinishedAsync(EmailStatuses.Sent, DeliveryStatuses.Sent);
        var otherTeam = await _fixture.AddTeamAsync();
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(ctx).ListDeliveriesAsync(otherTeam.Id, email.Id, TestContext.Current.CancellationToken));
    }

    public void Dispose() => _fixture.Dispose();

    #region Private methods

    private EmailSendService CreateService(AndesMailerDbContext ctx) => new(ctx, _signal, _fixture.Time);

    private static string Version(Email email) => Convert.ToBase64String(email.Version);

    private Task<(Guid TeamId, Guid SenderId, Email Email)> SeedDraftAsync(params string[] addresses) =>
        SeedDraftAsync(EmailStatuses.Draft, "Report", addresses);

    private async Task<(Guid TeamId, Guid SenderId, Email Email)> SeedDraftAsync(EmailStatuses status, string subject, params string[] addresses)
    {
        var team = await _fixture.AddTeamAsync();
        var sender = await _fixture.AddUserAsync($"{Guid.NewGuid():N}@contoso.com");
        await using var ctx = _fixture.CreateContext();
        var email = new Email { TeamId = team.Id, AuthorId = sender.Id, Subject = subject, BodyHtml = "<p>Hi</p>", StatusId = status };
        foreach (var (address, position) in addresses.Select((a, i) => (a, i)))
        {
            email.Recipients.Add(new EmailRecipient { Address = address, RecipientTypeId = RecipientTypes.To, Position = position });
        }

        ctx.Add(email);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (team.Id, sender.Id, email);
    }

    private async Task<(Guid TeamId, Email Email)> SeedScheduledAsync()
    {
        var (teamId, senderId, email) = await SeedDraftAsync("ada@contoso.com");
        await using var ctx = _fixture.CreateContext();
        var sent = await CreateService(ctx).SendAsync(teamId, email.Id, senderId, new SendEmailActionDto(Version(email), _fixture.Time.GetUtcNow().AddHours(1)), TestContext.Current.CancellationToken);

        await using var read = _fixture.CreateContext();

        return (teamId, await read.Emails.AsNoTracking().SingleAsync(e => e.Id == sent.Id, TestContext.Current.CancellationToken));
    }

    private async Task<(Guid TeamId, Email Email)> SeedFinishedAsync(EmailStatuses status, params DeliveryStatuses[] outcomes)
    {
        var (teamId, _, email) = await SeedDraftAsync(status, "Report", [.. outcomes.Select((_, i) => $"r{i}@contoso.com")]);
        await using var ctx = _fixture.CreateContext();
        var recipients = await ctx.EmailRecipients.Where(r => r.EmailId == email.Id).OrderBy(r => r.Position).ToListAsync(TestContext.Current.CancellationToken);
        foreach (var (recipient, outcome) in recipients.Zip(outcomes))
        {
            ctx.Add(new EmailDelivery
            {
                EmailId = email.Id,
                RecipientId = recipient.Id,
                StatusId = outcome,
                AttemptCount = outcome == DeliveryStatuses.Failed ? 3 : 1,
                LastError = outcome == DeliveryStatuses.Failed ? "The mail server failed (HTTP 500)." : null,
                NextAttemptAt = _fixture.Time.GetUtcNow(),
            });
        }

        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return (teamId, email);
    }

    private async Task AddAttachmentAsync(Guid emailId, AttachmentStatuses status)
    {
        await using var ctx = _fixture.CreateContext();
        var id = Guid.NewGuid();
        ctx.Add(new EmailAttachment
        {
            Id = id,
            EmailId = emailId,
            FileName = $"{id:N}.pdf",
            DocumentTypeId = DocumentTypes.Pdf,
            ContentType = "application/pdf",
            SizeBytes = 3,
            BlobName = $"emails/{id:N}",
            StatusId = status,
        });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<EmailDelivery>> ReadDeliveriesAsync(Guid emailId)
    {
        await using var ctx = _fixture.CreateContext();

        return await ctx.EmailDeliveries.AsNoTracking().Where(d => d.EmailId == emailId).ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<bool> WasSignalledAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        try
        {
            await _signal.WaitAsync(timeout.Token);

            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    #endregion
}
