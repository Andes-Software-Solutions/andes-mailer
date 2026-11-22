using System.Diagnostics;

using Andes.Mailer.Common.Constants;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Service.Emails.Sending;
using Andes.Mailer.Service.Emails.Sending.Exceptions;
using Andes.Mailer.Service.Emails.Sending.Models;
using Andes.Mailer.Service.Options;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Andes.Mailer.Unit.Test.Service.Emails.Sending;

public sealed class EmailDispatchProcessorTests : IAsyncDisposable
{
    private readonly MailerDbFixture _fixture = new(concurrent: true);
    private readonly IMailboxClient _mailbox = Substitute.For<IMailboxClient>();
    private readonly EmailDispatchSignal _signal = new();
    private readonly ServiceProvider _services;

    public EmailDispatchProcessorTests()
    {
        _services = new ServiceCollection()
            .AddScoped(_ => _fixture.CreateContext())
            .AddScoped(_ => _mailbox)
            .BuildServiceProvider();
        _mailbox.CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>())
            .Returns(_ => $"draft-{Guid.NewGuid():N}");
        _mailbox.DraftExistsAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    [Fact]
    public async Task DispatchOnceAsync_DueDeliveries_SendsEachRecipientItsOwnMessageAndCompletesTheEmail()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com", "bob@contoso.com");

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        var deliveries = await ReadDeliveriesAsync(email.Id);
        Assert.All(deliveries, d =>
        {
            Assert.Equal(DeliveryStatuses.Sent, d.StatusId);
            Assert.Equal(1, d.AttemptCount);
            Assert.NotNull(d.GraphMessageId);
            Assert.NotNull(d.DateSent);
            Assert.Null(d.LeaseExpiresAt);
        });
        await _mailbox.Received(1).CreateDraftAsync(email.Team!.ServiceAccountId, Arg.Is<OutgoingMessage>(m => m.RecipientAddress == "ada@contoso.com" && m.Subject == "Report"), Arg.Any<CancellationToken>());
        await _mailbox.Received(1).CreateDraftAsync(email.Team!.ServiceAccountId, Arg.Is<OutgoingMessage>(m => m.RecipientAddress == "bob@contoso.com"), Arg.Any<CancellationToken>());
        await _mailbox.Received(2).SendDraftAsync(email.Team!.ServiceAccountId, Arg.Any<string>(), Arg.Any<CancellationToken>());
        var stored = await ReadEmailAsync(email.Id);
        Assert.Equal(EmailStatuses.Sent, stored.StatusId);
        Assert.Equal(_fixture.Time.GetUtcNow(), stored.DateCompleted);
    }

    [Fact]
    public async Task DispatchOnceAsync_UploadedAttachment_GoesWithTheMessage()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        await using (var ctx = _fixture.CreateContext())
        {
            ctx.Add(new EmailAttachment
            {
                EmailId = email.Id,
                FileName = "report.pdf",
                DocumentTypeId = DocumentTypes.Pdf,
                ContentType = "application/pdf",
                SizeBytes = 3,
                BlobName = "emails/report",
                StatusId = AttachmentStatuses.Uploaded,
            });
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        await _mailbox.Received(1).CreateDraftAsync(
            Arg.Any<Guid>(),
            Arg.Is<OutgoingMessage>(m => m.Attachments.Count == 1 && m.Attachments[0].BlobName == "emails/report"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_ScheduledEmail_WaitsForItsTimeThenSends()
    {
        var at = _fixture.Time.GetUtcNow().AddMinutes(10);
        var email = await SeedAsync(EmailStatuses.Scheduled, at, "ada@contoso.com");
        var processor = CreateProcessor();

        await processor.DispatchOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(EmailStatuses.Scheduled, (await ReadEmailAsync(email.Id)).StatusId);
        await _mailbox.DidNotReceive().CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());

        _fixture.Time.Advance(TimeSpan.FromMinutes(10));
        await processor.DispatchOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DeliveryStatuses.Sent, Assert.Single(await ReadDeliveriesAsync(email.Id)).StatusId);
        Assert.Equal(EmailStatuses.Sent, (await ReadEmailAsync(email.Id)).StatusId);
    }

    [Fact]
    public async Task DispatchOnceAsync_TransientFailures_RetryWithGrowingDelayThenFailAfterTheLastAttempt()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        _mailbox.SendDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MailboxException(MailboxFailureKinds.Transient, "The mail server failed (HTTP 500)."));
        var processor = CreateProcessor();

        await processor.DispatchOnceAsync(TestContext.Current.CancellationToken);
        var first = Assert.Single(await ReadDeliveriesAsync(email.Id));
        Assert.Equal(DeliveryStatuses.Pending, first.StatusId);
        Assert.Equal(1, first.AttemptCount);
        Assert.Equal(_fixture.Time.GetUtcNow().AddMinutes(1), first.NextAttemptAt);
        Assert.Equal("The mail server failed (HTTP 500).", first.LastError);

        // Not due yet: nothing is tried before the delay.
        await processor.DispatchOnceAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, Assert.Single(await ReadDeliveriesAsync(email.Id)).AttemptCount);

        _fixture.Time.Advance(TimeSpan.FromMinutes(1));
        await processor.DispatchOnceAsync(TestContext.Current.CancellationToken);
        var second = Assert.Single(await ReadDeliveriesAsync(email.Id));
        Assert.Equal(2, second.AttemptCount);
        Assert.Equal(_fixture.Time.GetUtcNow().AddMinutes(2), second.NextAttemptAt);

        _fixture.Time.Advance(TimeSpan.FromMinutes(2));
        await processor.DispatchOnceAsync(TestContext.Current.CancellationToken);
        var last = Assert.Single(await ReadDeliveriesAsync(email.Id));
        Assert.Equal(DeliveryStatuses.Failed, last.StatusId);
        Assert.Equal(3, last.AttemptCount);

        // One draft, re-sent each time; never a second copy for the same recipient.
        await _mailbox.Received(1).CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());
        await _mailbox.Received(3).SendDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Equal(EmailStatuses.Failed, (await ReadEmailAsync(email.Id)).StatusId);

        _fixture.Time.Advance(TimeSpan.FromHours(1));
        await processor.DispatchOnceAsync(TestContext.Current.CancellationToken);
        await _mailbox.Received(3).SendDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_PermanentRejection_FailsAtOnceAndDiscardsTheDraft()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com", "bad@contoso.com");
        _mailbox.SendDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _mailbox.CreateDraftAsync(Arg.Any<Guid>(), Arg.Is<OutgoingMessage>(m => m.RecipientAddress == "bad@contoso.com"), Arg.Any<CancellationToken>())
            .Returns("draft-bad");
        _mailbox.SendDraftAsync(Arg.Any<Guid>(), "draft-bad", Arg.Any<CancellationToken>())
            .ThrowsAsync(new MailboxException(MailboxFailureKinds.Permanent, "The mail server rejected the message (ErrorInvalidRecipients)."));

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        var deliveries = await ReadDeliveriesAsync(email.Id);
        var failed = Assert.Single(deliveries, d => d.StatusId == DeliveryStatuses.Failed);
        Assert.Equal(1, failed.AttemptCount);
        Assert.Null(failed.GraphMessageId);
        Assert.Contains("ErrorInvalidRecipients", failed.LastError, StringComparison.Ordinal);
        Assert.Single(deliveries, d => d.StatusId == DeliveryStatuses.Sent);
        await _mailbox.Received(1).DeleteDraftAsync(Arg.Any<Guid>(), "draft-bad", Arg.Any<CancellationToken>());
        Assert.Equal(EmailStatuses.PartiallySent, (await ReadEmailAsync(email.Id)).StatusId);
    }

    [Fact]
    public async Task DispatchOnceAsync_Throttled_DefersWithoutCountingTheAttemptAndStopsSendingFromTheMailbox()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com", "bob@contoso.com");
        _mailbox.CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new MailboxException(MailboxFailureKinds.Throttled, "The mail server is throttling the mailbox (ApplicationThrottled).", TimeSpan.FromSeconds(30)));

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        var deliveries = await ReadDeliveriesAsync(email.Id);
        Assert.All(deliveries, d =>
        {
            Assert.Equal(DeliveryStatuses.Pending, d.StatusId);
            Assert.Equal(0, d.AttemptCount);
        });
        Assert.Single(deliveries, d => d.NextAttemptAt == _fixture.Time.GetUtcNow().AddSeconds(30));
        await _mailbox.Received(1).CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());
        Assert.Equal(EmailStatuses.Sending, (await ReadEmailAsync(email.Id)).StatusId);
    }

    [Fact]
    public async Task DispatchOnceAsync_EarlierSendWentThroughUnrecorded_MarksSentWithoutSendingAgain()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        await UpdateDeliveryAsync(email.Id, d => d.GraphMessageId = "draft-earlier");
        _mailbox.DraftExistsAsync(Arg.Any<Guid>(), "draft-earlier", Arg.Any<CancellationToken>()).Returns(false);

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DeliveryStatuses.Sent, Assert.Single(await ReadDeliveriesAsync(email.Id)).StatusId);
        await _mailbox.DidNotReceive().CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());
        await _mailbox.DidNotReceive().SendDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_EarlierDraftStillUnsent_SendsThatDraft()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        await UpdateDeliveryAsync(email.Id, d => d.GraphMessageId = "draft-earlier");

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DeliveryStatuses.Sent, Assert.Single(await ReadDeliveriesAsync(email.Id)).StatusId);
        await _mailbox.DidNotReceive().CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());
        await _mailbox.Received(1).SendDraftAsync(Arg.Any<Guid>(), "draft-earlier", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_ClaimRanOutWithAttemptsLeft_PicksItUpAndCountsTheAttempt()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        await UpdateDeliveryAsync(email.Id, d =>
        {
            d.StatusId = DeliveryStatuses.Sending;
            d.AttemptCount = 1;
            d.LeaseExpiresAt = _fixture.Time.GetUtcNow().AddSeconds(-1);
        });

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        var delivery = Assert.Single(await ReadDeliveriesAsync(email.Id));
        Assert.Equal(DeliveryStatuses.Sent, delivery.StatusId);
        Assert.Equal(2, delivery.AttemptCount);
    }

    [Fact]
    public async Task DispatchOnceAsync_ClaimStillHeld_LeavesItAlone()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        await UpdateDeliveryAsync(email.Id, d =>
        {
            d.StatusId = DeliveryStatuses.Sending;
            d.AttemptCount = 1;
            d.LeaseExpiresAt = _fixture.Time.GetUtcNow().AddMinutes(1);
        });

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DeliveryStatuses.Sending, Assert.Single(await ReadDeliveriesAsync(email.Id)).StatusId);
        await _mailbox.DidNotReceive().CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_ClaimRanOutOnTheLastAttempt_FailsWithoutSending()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        await UpdateDeliveryAsync(email.Id, d =>
        {
            d.StatusId = DeliveryStatuses.Sending;
            d.AttemptCount = 3;
            d.LeaseExpiresAt = _fixture.Time.GetUtcNow().AddSeconds(-1);
        });

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        var delivery = Assert.Single(await ReadDeliveriesAsync(email.Id));
        Assert.Equal(DeliveryStatuses.Failed, delivery.StatusId);
        Assert.Equal(3, delivery.AttemptCount);
        await _mailbox.DidNotReceive().CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());
        Assert.Equal(EmailStatuses.Failed, (await ReadEmailAsync(email.Id)).StatusId);
    }

    [Fact]
    public async Task DispatchOnceAsync_SentDelivery_IsNeverSentAgain()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        var processor = CreateProcessor();

        await processor.DispatchOnceAsync(TestContext.Current.CancellationToken);
        _fixture.Time.Advance(TimeSpan.FromDays(1));
        await processor.DispatchOnceAsync(TestContext.Current.CancellationToken);

        await _mailbox.Received(1).SendDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Equal(1, Assert.Single(await ReadDeliveriesAsync(email.Id)).AttemptCount);
    }

    [Fact]
    public async Task DispatchOnceAsync_MoreThanTheMailboxLimit_SendsTheRestOnTheNextRun()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "a@contoso.com", "b@contoso.com", "c@contoso.com");
        var processor = CreateProcessor(maxPerMailboxPerRun: 2);

        await processor.DispatchOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, (await ReadDeliveriesAsync(email.Id)).Count(d => d.StatusId == DeliveryStatuses.Sent));
        Assert.Equal(EmailStatuses.Sending, (await ReadEmailAsync(email.Id)).StatusId);

        await processor.DispatchOnceAsync(TestContext.Current.CancellationToken);

        Assert.All(await ReadDeliveriesAsync(email.Id), d => Assert.Equal(DeliveryStatuses.Sent, d.StatusId));
        Assert.Equal(EmailStatuses.Sent, (await ReadEmailAsync(email.Id)).StatusId);
    }

    [Fact]
    public async Task DispatchOnceAsync_TwoRunsAtOnce_SendEachDeliveryOnce()
    {
        var email = await SeedAsync(EmailStatuses.Sending, [.. Enumerable.Range(0, 6).Select(i => $"r{i}@contoso.com")]);

        await Task.WhenAll(
            CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken),
            CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken));

        await _mailbox.Received(6).SendDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.All(await ReadDeliveriesAsync(email.Id), d =>
        {
            Assert.Equal(DeliveryStatuses.Sent, d.StatusId);
            Assert.Equal(1, d.AttemptCount);
        });
    }

    [Fact]
    public async Task DispatchOnceAsync_SendRejectedButDraftGone_RecordsSentWithoutAnotherCopy()
    {
        // A retried POST /send whose first request went through answers 404; the draft being gone shows the message went out.
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        _mailbox.CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>()).Returns("draft-sent");
        _mailbox.SendDraftAsync(Arg.Any<Guid>(), "draft-sent", Arg.Any<CancellationToken>())
            .ThrowsAsync(new MailboxException(MailboxFailureKinds.Permanent, "The mail server rejected the message (ErrorItemNotFound)."));
        _mailbox.DraftExistsAsync(Arg.Any<Guid>(), "draft-sent", Arg.Any<CancellationToken>()).Returns(false);

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        var delivery = Assert.Single(await ReadDeliveriesAsync(email.Id));
        Assert.Equal(DeliveryStatuses.Sent, delivery.StatusId);
        Assert.Equal("draft-sent", delivery.GraphMessageId);
        await _mailbox.DidNotReceive().DeleteDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_SendRejectedAndDraftUncheckable_FailsButKeepsTheDraftId()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        _mailbox.CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>()).Returns("draft-unknown");
        _mailbox.SendDraftAsync(Arg.Any<Guid>(), "draft-unknown", Arg.Any<CancellationToken>())
            .ThrowsAsync(new MailboxException(MailboxFailureKinds.Permanent, "The mail server rejected the message (ErrorAccessDenied)."));
        _mailbox.DraftExistsAsync(Arg.Any<Guid>(), "draft-unknown", Arg.Any<CancellationToken>())
            .ThrowsAsync(new MailboxException(MailboxFailureKinds.Permanent, "The mail server rejected the message (ErrorAccessDenied)."));

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        var delivery = Assert.Single(await ReadDeliveriesAsync(email.Id));
        Assert.Equal(DeliveryStatuses.Failed, delivery.StatusId);
        Assert.Equal("draft-unknown", delivery.GraphMessageId);
        await _mailbox.DidNotReceive().DeleteDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_LastAttemptTimedOutButWentThrough_RecordsSent()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        await UpdateDeliveryAsync(email.Id, d =>
        {
            d.AttemptCount = 2;
            d.GraphMessageId = "draft-late";
        });
        _mailbox.DraftExistsAsync(Arg.Any<Guid>(), "draft-late", Arg.Any<CancellationToken>()).Returns(true, false);
        _mailbox.SendDraftAsync(Arg.Any<Guid>(), "draft-late", Arg.Any<CancellationToken>())
            .ThrowsAsync(new MailboxException(MailboxFailureKinds.Transient, "The mail server did not answer in time."));

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        var delivery = Assert.Single(await ReadDeliveriesAsync(email.Id));
        Assert.Equal(DeliveryStatuses.Sent, delivery.StatusId);
        Assert.Equal(3, delivery.AttemptCount);
        Assert.Equal(EmailStatuses.Sent, (await ReadEmailAsync(email.Id)).StatusId);
    }

    [Theory]
    [InlineData(false, DeliveryStatuses.Sent)]
    [InlineData(true, DeliveryStatuses.Failed)]
    public async Task DispatchOnceAsync_ClaimRanOutOnTheLastAttemptWithADraft_ChecksTheDraftBeforeSettling(bool draftStillThere, DeliveryStatuses expected)
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        await UpdateDeliveryAsync(email.Id, d =>
        {
            d.StatusId = DeliveryStatuses.Sending;
            d.AttemptCount = 3;
            d.LeaseExpiresAt = _fixture.Time.GetUtcNow().AddSeconds(-1);
            d.GraphMessageId = "draft-interrupted";
        });
        _mailbox.DraftExistsAsync(Arg.Any<Guid>(), "draft-interrupted", Arg.Any<CancellationToken>()).Returns(draftStillThere);

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        var delivery = Assert.Single(await ReadDeliveriesAsync(email.Id));
        Assert.Equal(expected, delivery.StatusId);
        Assert.Equal("draft-interrupted", delivery.GraphMessageId);
        await _mailbox.DidNotReceive().SendDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mailbox.DidNotReceive().CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_TeamChangedMailboxSinceTheDraft_ChecksTheOldMailboxThenSendsFromTheNewOne()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        var oldMailbox = Guid.NewGuid();
        await UpdateDeliveryAsync(email.Id, d =>
        {
            d.GraphMessageId = "draft-old";
            d.DraftMailboxId = oldMailbox;
        });
        _mailbox.CreateDraftAsync(email.Team!.ServiceAccountId, Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>()).Returns("draft-new");

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        var delivery = Assert.Single(await ReadDeliveriesAsync(email.Id));
        Assert.Equal(DeliveryStatuses.Sent, delivery.StatusId);
        Assert.Equal("draft-new", delivery.GraphMessageId);
        Assert.Equal(email.Team!.ServiceAccountId, delivery.DraftMailboxId);
        await _mailbox.Received(1).DraftExistsAsync(oldMailbox, "draft-old", Arg.Any<CancellationToken>());
        await _mailbox.Received(1).DeleteDraftAsync(oldMailbox, "draft-old", Arg.Any<CancellationToken>());
        await _mailbox.Received(1).SendDraftAsync(email.Team!.ServiceAccountId, "draft-new", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_TeamChangedMailboxAfterTheSendWentThrough_RecordsSentWithoutSendingAgain()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        var oldMailbox = Guid.NewGuid();
        await UpdateDeliveryAsync(email.Id, d =>
        {
            d.GraphMessageId = "draft-old";
            d.DraftMailboxId = oldMailbox;
        });
        _mailbox.DraftExistsAsync(oldMailbox, "draft-old", Arg.Any<CancellationToken>()).Returns(false);

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DeliveryStatuses.Sent, Assert.Single(await ReadDeliveriesAsync(email.Id)).StatusId);
        await _mailbox.DidNotReceive().CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());
        await _mailbox.DidNotReceive().SendDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_DraftMailboxGone_FailsAndKeepsTheDraftIdWithoutSending()
    {
        // The team retired its old mailbox and someone retried: the old mailbox's 404 must not read as "sent".
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        var oldMailbox = Guid.NewGuid();
        await UpdateDeliveryAsync(email.Id, d =>
        {
            d.GraphMessageId = "draft-old";
            d.DraftMailboxId = oldMailbox;
        });
        _mailbox.DraftExistsAsync(oldMailbox, "draft-old", Arg.Any<CancellationToken>())
            .ThrowsAsync(new MailboxException(MailboxFailureKinds.Permanent, "The mail server rejected the message (ErrorInvalidUser)."));

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        var delivery = Assert.Single(await ReadDeliveriesAsync(email.Id));
        Assert.Equal(DeliveryStatuses.Failed, delivery.StatusId);
        Assert.Null(delivery.DateSent);
        Assert.Equal("draft-old", delivery.GraphMessageId);
        Assert.Equal(oldMailbox, delivery.DraftMailboxId);
        await _mailbox.DidNotReceive().CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());
        await _mailbox.DidNotReceive().SendDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mailbox.DidNotReceive().DeleteDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchOnceAsync_CheckOnlyClaimRanOut_FailsWithoutAnotherClaim()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        await UpdateDeliveryAsync(email.Id, d =>
        {
            d.StatusId = DeliveryStatuses.Sending;
            d.AttemptCount = 4;
            d.LeaseExpiresAt = _fixture.Time.GetUtcNow().AddSeconds(-1);
            d.GraphMessageId = "draft-interrupted";
        });

        await CreateProcessor().DispatchOnceAsync(TestContext.Current.CancellationToken);

        var delivery = Assert.Single(await ReadDeliveriesAsync(email.Id));
        Assert.Equal(DeliveryStatuses.Failed, delivery.StatusId);
        Assert.Equal(4, delivery.AttemptCount);
        Assert.Equal("Sending was interrupted too many times.", delivery.LastError);
        Assert.Equal("draft-interrupted", delivery.GraphMessageId);
        Assert.Null(delivery.LeaseExpiresAt);
        await _mailbox.DidNotReceive().DraftExistsAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _mailbox.DidNotReceive().CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());
        await _mailbox.DidNotReceive().SendDraftAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        Assert.Equal(EmailStatuses.Failed, (await ReadEmailAsync(email.Id)).StatusId);
    }

    [Fact]
    public async Task ExecuteAsync_ScheduleMonthsApart_KeepsRunningAndStillWakesOnSignal()
    {
        // Once a year: the next run is far beyond the longest timeout WaitAsync accepts.
        using var processor = CreateProcessor(schedule: "0 0 1 1 *");
        await processor.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);

        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        _signal.Notify();
        var sent = await WaitForAsync(email.Id, DeliveryStatuses.Sent);
        var faulted = processor.ExecuteTask?.IsFaulted;
        await processor.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DeliveryStatuses.Sent, sent);
        Assert.False(faulted);
    }

    [Fact]
    public async Task ExecuteAsync_Started_RunsAtOnceWithoutWaitingForTheSchedule()
    {
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        using var processor = CreateProcessor();

        await processor.StartAsync(TestContext.Current.CancellationToken);
        var sent = await WaitForAsync(email.Id, DeliveryStatuses.Sent);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DeliveryStatuses.Sent, sent);
    }

    [Fact]
    public async Task ExecuteAsync_Started_TracesTheRunWithEachDeliveryUnderIt()
    {
        using var activities = new ActivityCollector();
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        var deliveryId = Assert.Single(await ReadDeliveriesAsync(email.Id)).Id;
        using var processor = CreateProcessor();

        await processor.StartAsync(TestContext.Current.CancellationToken);
        var delivery = await activities.WaitForAsync(a =>
            a.OperationName == TelemetryNames.EmailDeliveryActivity && Equals(a.GetTagItem(TelemetryNames.DeliveryIdTag), deliveryId));
        var run = await activities.WaitForAsync(a => a.OperationName == TelemetryNames.EmailDispatchActivity && a.SpanId == delivery.ParentSpanId);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(run.TraceId, delivery.TraceId);
        Assert.Equal(default, run.ParentSpanId);
        Assert.Equal(ActivityStatusCode.Unset, delivery.Status);
    }

    [Fact]
    public async Task ExecuteAsync_Signalled_RunsBeforeTheNextScheduledTime()
    {
        using var processor = CreateProcessor();
        await processor.StartAsync(TestContext.Current.CancellationToken);

        // Seeded after the first run, so only the wake-up can send it: the fake clock never reaches the next minute.
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        var email = await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        _signal.Notify();
        var sent = await WaitForAsync(email.Id, DeliveryStatuses.Sent);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(DeliveryStatuses.Sent, sent);
    }

    [Fact]
    public async Task ExecuteAsync_Disabled_SendsNothing()
    {
        await SeedAsync(EmailStatuses.Sending, "ada@contoso.com");
        using var processor = CreateProcessor(enabled: false);

        await processor.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        await processor.StopAsync(TestContext.Current.CancellationToken);

        await _mailbox.DidNotReceive().CreateDraftAsync(Arg.Any<Guid>(), Arg.Any<OutgoingMessage>(), Arg.Any<CancellationToken>());
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        _fixture.Dispose();
    }

    #region Private methods

    private EmailDispatchProcessor CreateProcessor(bool enabled = true, int maxPerMailboxPerRun = 30, string schedule = "* * * * *") =>
        new(
            _services.GetRequiredService<IServiceScopeFactory>(),
            _signal,
            _fixture.Time,
            Microsoft.Extensions.Options.Options.Create(new EmailSendingOptions { Enabled = enabled, MaxPerMailboxPerRun = maxPerMailboxPerRun, Schedule = schedule }),
            NullLogger<EmailDispatchProcessor>.Instance);

    private Task<Email> SeedAsync(EmailStatuses status, params string[] addresses) =>
        SeedAsync(status, _fixture.Time.GetUtcNow(), addresses);

    private async Task<Email> SeedAsync(EmailStatuses status, DateTimeOffset dueAt, params string[] addresses)
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync($"{Guid.NewGuid():N}@contoso.com");
        await using var ctx = _fixture.CreateContext();
        var email = new Email
        {
            TeamId = team.Id,
            AuthorId = author.Id,
            Subject = "Report",
            BodyHtml = "<p>Hi</p>",
            StatusId = status,
            ScheduledFor = status == EmailStatuses.Scheduled ? dueAt : null,
            SentById = author.Id,
            DateSendRequested = _fixture.Time.GetUtcNow(),
        };
        foreach (var (address, position) in addresses.Select((a, i) => (a, i)))
        {
            var recipient = new EmailRecipient { Address = address, RecipientTypeId = RecipientTypes.To, Position = position };
            email.Recipients.Add(recipient);
            email.Deliveries.Add(new EmailDelivery { Recipient = recipient, StatusId = DeliveryStatuses.Pending, NextAttemptAt = dueAt });
        }

        ctx.Add(email);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        email.Team = team;

        return email;
    }

    private async Task UpdateDeliveryAsync(Guid emailId, Action<EmailDelivery> change)
    {
        await using var ctx = _fixture.CreateContext();
        var delivery = await ctx.EmailDeliveries.SingleAsync(d => d.EmailId == emailId, TestContext.Current.CancellationToken);
        change(delivery);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<List<EmailDelivery>> ReadDeliveriesAsync(Guid emailId)
    {
        await using var ctx = _fixture.CreateContext();

        return await ctx.EmailDeliveries
            .AsNoTracking()
            .Where(d => d.EmailId == emailId)
            .OrderBy(d => d.Recipient!.Position)
            .ToListAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Email> ReadEmailAsync(Guid emailId)
    {
        await using var ctx = _fixture.CreateContext();

        return await ctx.Emails.AsNoTracking().SingleAsync(e => e.Id == emailId, TestContext.Current.CancellationToken);
    }

    private async Task<DeliveryStatuses> WaitForAsync(Guid emailId, DeliveryStatuses status)
    {
        for (var i = 0; i < 100; i++)
        {
            var delivery = Assert.Single(await ReadDeliveriesAsync(emailId));
            if (delivery.StatusId == status)
            {
                return delivery.StatusId;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }

        return Assert.Single(await ReadDeliveriesAsync(emailId)).StatusId;
    }

    #endregion
}
