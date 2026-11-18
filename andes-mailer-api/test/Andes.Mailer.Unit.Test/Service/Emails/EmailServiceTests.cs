using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Entity.Emails.Templates;
using Andes.Mailer.Repository.Blob;
using Andes.Mailer.Repository.Blob.Exceptions;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails;
using Andes.Mailer.Service.Emails.Exceptions;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Andes.Mailer.Unit.Test.Service.Emails;

public sealed class EmailServiceTests : IDisposable
{
    private readonly MailerDbFixture _fixture = new();
    private readonly IDirectoryUserService _directory = Substitute.For<IDirectoryUserService>();
    private readonly IAttachmentStore _store = Substitute.For<IAttachmentStore>();

    public EmailServiceTests() =>
        _directory.FindByEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, DirectoryUser>());

    [Fact]
    public async Task CreateAsync_Draft_StoresSanitizedBodyAndResolvedRecipientsInOrder()
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var graceId = Guid.NewGuid();
        _directory.FindByEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, DirectoryUser> { ["grace@contoso.com"] = new(graceId, "Grace", "Hopper", "grace@contoso.com") });
        var action = new CreateEmailActionDto(
            " Hello ",
            "<p>Hi</p><script>alert(1)</script>",
            [new("zed@external.org", RecipientTypes.Cc), new(" GRACE@contoso.com ", RecipientTypes.To), new("amy@external.org", RecipientTypes.To)]);
        await using var ctx = _fixture.CreateContext();

        var created = await CreateService(ctx).CreateAsync(team.Id, author.Id, action, TestContext.Current.CancellationToken);
        await using var readCtx = _fixture.CreateContext();
        var reloaded = await CreateService(readCtx).GetAsync(team.Id, created.Id, TestContext.Current.CancellationToken);

        Assert.Equal(author.Id, reloaded.AuthorId);
        Assert.Equal("Hello", reloaded.Subject);
        Assert.Equal("<p>Hi</p>", reloaded.BodyHtml);
        Assert.Equal(EmailStatuses.Draft, reloaded.Status);
        Assert.Equal(
            [
                new EmailRecipientDto("grace@contoso.com", RecipientTypes.To, graceId, "Grace Hopper"),
                new EmailRecipientDto("amy@external.org", RecipientTypes.To, null, null),
                new EmailRecipientDto("zed@external.org", RecipientTypes.Cc, null, null),
            ],
            reloaded.Recipients);
        Assert.Equal(created.Recipients, reloaded.Recipients);
    }

    [Fact]
    public async Task GetAsync_AnotherTeamsEmail_ThrowsNotFound()
    {
        var team = await _fixture.AddTeamAsync();
        var otherTeam = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var email = await _fixture.AddEmailAsync(team.Id, author.Id);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(ctx).GetAsync(otherTeam.Id, email.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_TeammatesDraft_ReplacesContentAndKeepsAuthor()
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var created = await CreateDraftAsync(team.Id, author.Id);
        await using var ctx = _fixture.CreateContext();

        var updated = await CreateService(ctx).UpdateAsync(team.Id, created.Id, new UpdateEmailActionDto("Second", "<p>Two</p>", [], created.Version), TestContext.Current.CancellationToken);

        Assert.Equal("Second", updated.Subject);
        Assert.Equal(author.Id, updated.AuthorId);
    }

    [Fact]
    public async Task UpdateAsync_AnotherTeamsDraft_ThrowsNotFound()
    {
        var team = await _fixture.AddTeamAsync();
        var otherTeam = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var created = await CreateDraftAsync(team.Id, author.Id);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService(ctx).UpdateAsync(otherTeam.Id, created.Id, new UpdateEmailActionDto("x", string.Empty, [], created.Version), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_ChangedRecipients_KeepsStayingRowsAndResolvesOnlyNewAddresses()
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var created = await CreateDraftAsync(team.Id, author.Id, new EmailRecipientActionDto("amy@external.org", RecipientTypes.To), new EmailRecipientActionDto("bob@external.org", RecipientTypes.Cc));
        _directory.ClearReceivedCalls();
        await using var ctx = _fixture.CreateContext();
        var action = new UpdateEmailActionDto("Second", "<p>Two</p>", [new("bob@external.org", RecipientTypes.To), new("cat@external.org", RecipientTypes.Bcc)], created.Version);

        var updated = await CreateService(ctx).UpdateAsync(team.Id, created.Id, action, TestContext.Current.CancellationToken);

        Assert.Equal(["bob@external.org", "cat@external.org"], updated.Recipients.Select(r => r.Address));
        Assert.Equal([RecipientTypes.To, RecipientTypes.Bcc], updated.Recipients.Select(r => r.Type));
        await _directory.Received(1).FindByEmailsAsync(
            Arg.Is<IReadOnlyCollection<string>>(a => a.Count == 1 && a.Contains("cat@external.org")),
            Arg.Any<CancellationToken>());
        await using var readCtx = _fixture.CreateContext();
        Assert.Equal(2, await readCtx.EmailRecipients.CountAsync(r => r.EmailId == created.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_StaleVersion_ThrowsConflict()
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var created = await CreateDraftAsync(team.Id, author.Id);
        await using var ctx = _fixture.CreateContext();
        var stale = Convert.ToBase64String(new byte[8]);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(ctx).UpdateAsync(team.Id, created.Id, new UpdateEmailActionDto("x", string.Empty, [], stale), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_SentEmail_ThrowsNotEditable()
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var email = await _fixture.AddEmailAsync(team.Id, author.Id, EmailStatuses.Sent);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<EmailNotEditableException>(() =>
            CreateService(ctx).UpdateAsync(team.Id, email.Id, new UpdateEmailActionDto("x", string.Empty, [], Convert.ToBase64String(email.Version)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_TeamEmail_DeactivatesItAndHidesItsRecipients()
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var created = await CreateDraftAsync(team.Id, author.Id, new EmailRecipientActionDto("amy@external.org", RecipientTypes.To));
        await using var ctx = _fixture.CreateContext();

        await CreateService(ctx).DeleteAsync(team.Id, created.Id, TestContext.Current.CancellationToken);

        await using var readCtx = _fixture.CreateContext();
        var row = await readCtx.Emails.IgnoreQueryFilters().SingleAsync(e => e.Id == created.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_fixture.Time.GetUtcNow(), row.DateDeactivated);
        Assert.False(await readCtx.EmailRecipients.AnyAsync(r => r.EmailId == created.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListAsync_TeamEmails_ReturnsEveryAuthorsOfTheTeamOnlyMostRecentFirst()
    {
        var team = await _fixture.AddTeamAsync();
        var otherTeam = await _fixture.AddTeamAsync();
        var ada = await _fixture.AddUserAsync("ada@contoso.com");
        var bob = await _fixture.AddUserAsync("bob@contoso.com");
        var older = await _fixture.AddEmailAsync(team.Id, ada.Id);
        _fixture.Time.Advance(TimeSpan.FromMinutes(1));
        var newer = await _fixture.AddEmailAsync(team.Id, bob.Id);
        await _fixture.AddEmailAsync(otherTeam.Id, ada.Id);
        await using var ctx = _fixture.CreateContext();

        var page = await CreateService(ctx).ListAsync(team.Id, new ListEmailsActionDto(), TestContext.Current.CancellationToken);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal([newer.Id, older.Id], page.Items.Select(e => e.Id));
        Assert.Equal([bob.Id, ada.Id], page.Items.Select(e => e.AuthorId));
    }

    [Fact]
    public async Task GetAsync_DeactivatedTeam_ThrowsNotFound()
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var email = await _fixture.AddEmailAsync(team.Id, author.Id);
        await using (var deactivateCtx = _fixture.CreateContext())
        {
            deactivateCtx.Remove(await deactivateCtx.Teams.SingleAsync(t => t.Id == team.Id, TestContext.Current.CancellationToken));
            await deactivateCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(ctx).GetAsync(team.Id, email.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateFromTemplateAsync_Template_CopiesContentResolvesRecipientsAndLinksTheTemplate()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var member = await _fixture.AddUserAsync("bob@contoso.com");
        var template = await _fixture.AddTemplateAsync(team.Id, admin.Id);
        await AddRecipientsAsync(template.Id, ("zed@external.org", RecipientTypes.Cc), ("grace@contoso.com", RecipientTypes.To));
        var graceId = Guid.NewGuid();
        _directory.FindByEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, DirectoryUser> { ["grace@contoso.com"] = new(graceId, "Grace", "Hopper", "grace@contoso.com") });
        await using var ctx = _fixture.CreateContext();

        var email = await CreateFromTemplateAsync(ctx, team.Id, member.Id, template.Id);

        Assert.Equal(template.Id, email.TemplateId);
        Assert.Equal(member.Id, email.AuthorId);
        Assert.Equal(EmailStatuses.Draft, email.Status);
        Assert.Equal(template.Subject, email.Subject);
        Assert.Equal(template.BodyHtml, email.BodyHtml);
        Assert.Equal(
            [new EmailRecipientDto("grace@contoso.com", RecipientTypes.To, graceId, "Grace Hopper"), new EmailRecipientDto("zed@external.org", RecipientTypes.Cc, null, null)],
            email.Recipients);
        await using var readCtx = _fixture.CreateContext();
        var stored = await readCtx.Emails.SingleAsync(e => e.Id == email.Id, TestContext.Current.CancellationToken);
        Assert.Equal(template.Id, stored.TemplateId);
    }

    [Fact]
    public async Task CreateFromTemplateAsync_StoredAndFailedAttachments_CopiesOnlyStoredOnesUnderTheEmail()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var stored = NewAttachment(AttachmentStatuses.Uploaded);
        var failed = NewAttachment(AttachmentStatuses.Failed);
        var template = await _fixture.AddTemplateAsync(team.Id, admin.Id, null, stored, failed);
        await using var ctx = _fixture.CreateContext();

        var email = await CreateFromTemplateAsync(ctx, team.Id, admin.Id, template.Id);

        var copy = Assert.Single(email.Attachments);
        Assert.NotEqual(stored.Id, copy.Id);
        Assert.Equal(stored.FileName, copy.FileName);
        Assert.Equal(AttachmentStatuses.Uploaded, copy.Status);
        await using var readCtx = _fixture.CreateContext();
        var row = await readCtx.EmailAttachments.SingleAsync(a => a.EmailId == email.Id, TestContext.Current.CancellationToken);
        Assert.Equal($"{email.Id:N}/{copy.Id:N}", row.BlobName);
        await _store.Received(1).CopyAsync(stored.BlobName, row.BlobName, Arg.Any<CancellationToken>());
        await _store.DidNotReceive().CopyAsync(failed.BlobName, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(AttachmentStatuses.Pending)]
    [InlineData(AttachmentStatuses.Uploading)]
    public async Task CreateFromTemplateAsync_AttachmentStillUploading_ThrowsNotReadyAndSavesNothing(AttachmentStatuses status)
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var template = await _fixture.AddTemplateAsync(team.Id, admin.Id, null, NewAttachment(status));
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<TemplateNotReadyException>(() => CreateFromTemplateAsync(ctx, team.Id, admin.Id, template.Id));

        await using var readCtx = _fixture.CreateContext();
        Assert.False(await readCtx.Emails.AnyAsync(TestContext.Current.CancellationToken));
        await _store.DidNotReceive().CopyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFromTemplateAsync_AnotherTeamsTemplate_ThrowsNotFound()
    {
        var team = await _fixture.AddTeamAsync();
        var otherTeam = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var template = await _fixture.AddTemplateAsync(otherTeam.Id, admin.Id);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateFromTemplateAsync(ctx, team.Id, admin.Id, template.Id));
    }

    [Fact]
    public async Task CreateFromTemplateAsync_DeactivatedTemplate_ThrowsNotFound()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var template = await _fixture.AddTemplateAsync(team.Id, admin.Id);
        await using (var deleteCtx = _fixture.CreateContext())
        {
            deleteCtx.Remove(await deleteCtx.EmailTemplates.SingleAsync(t => t.Id == template.Id, TestContext.Current.CancellationToken));
            await deleteCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateFromTemplateAsync(ctx, team.Id, admin.Id, template.Id));
    }

    [Fact]
    public async Task CreateFromTemplateAsync_SecondCopyFails_DeletesTheFirstCopyAndSavesNoEmail()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var first = NewAttachment(AttachmentStatuses.Uploaded);
        var second = NewAttachment(AttachmentStatuses.Uploaded);
        var template = await _fixture.AddTemplateAsync(team.Id, admin.Id, null, first);

        // Attached later, so it is copied second: copies run oldest first.
        _fixture.Time.Advance(TimeSpan.FromMinutes(1));
        await using (var attachCtx = _fixture.CreateContext())
        {
            second.TemplateId = template.Id;
            attachCtx.Add(second);
            await attachCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        List<string> copies = [];
        _store.CopyAsync(first.BlobName, Arg.Do<string>(copies.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _store.CopyAsync(second.BlobName, Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new BlobNotFoundException(second.BlobName));
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateFromTemplateAsync(ctx, team.Id, admin.Id, template.Id));

        var copied = Assert.Single(copies);
        await _store.Received(1).DeleteIfExistsAsync(copied, Arg.Any<CancellationToken>());
        await using var readCtx = _fixture.CreateContext();
        Assert.False(await readCtx.Emails.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateFromTemplateAsync_TemplateChangedAfterwards_LeavesTheEmailAsItWasMade()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var template = await _fixture.AddTemplateAsync(team.Id, admin.Id);
        EmailDto email;
        await using (var ctx = _fixture.CreateContext())
        {
            email = await CreateFromTemplateAsync(ctx, team.Id, admin.Id, template.Id);
        }

        await using (var changeCtx = _fixture.CreateContext())
        {
            var row = await changeCtx.EmailTemplates.SingleAsync(t => t.Id == template.Id, TestContext.Current.CancellationToken);
            row.Subject = "Changed";
            await changeCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var readCtx = _fixture.CreateContext();
        var readBack = await CreateService(readCtx).GetAsync(team.Id, email.Id, TestContext.Current.CancellationToken);
        Assert.Equal(template.Subject, readBack.Subject);
    }

    [Fact]
    public async Task CreateFromTemplateAsync_ThenUpdate_BehavesLikeAnyDraft()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var template = await _fixture.AddTemplateAsync(team.Id, admin.Id);
        EmailDto email;
        await using (var ctx = _fixture.CreateContext())
        {
            email = await CreateFromTemplateAsync(ctx, team.Id, admin.Id, template.Id);
        }

        await using var updateCtx = _fixture.CreateContext();
        var updated = await CreateService(updateCtx).UpdateAsync(team.Id, email.Id, new UpdateEmailActionDto("Edited", "<p>Edited</p>", [], email.Version), TestContext.Current.CancellationToken);

        Assert.Equal("Edited", updated.Subject);
        Assert.Equal(template.Id, updated.TemplateId);
    }

    [Fact]
    public async Task CreateFromTemplateAsync_CopyCutShort_DeletesItsPossiblyCreatedDestination()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var stored = NewAttachment(AttachmentStatuses.Uploaded);
        var template = await _fixture.AddTemplateAsync(team.Id, admin.Id, null, stored);
        string? destination = null;
        _store.CopyAsync(stored.BlobName, Arg.Do<string>(d => destination = d), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateFromTemplateAsync(ctx, team.Id, admin.Id, template.Id));

        Assert.NotNull(destination);
        await _store.Received(1).DeleteIfExistsAsync(destination, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateFromTemplateAsync_SaveFailsAfterItsCommit_KeepsTheCopiesOfTheSavedEmail()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var template = await _fixture.AddTemplateAsync(team.Id, admin.Id, null, NewAttachment(AttachmentStatuses.Uploaded));
        await using var ctx = _fixture.CreateContext(new FailAfterCommit());

        await Assert.ThrowsAsync<TimeoutException>(() => CreateFromTemplateAsync(ctx, team.Id, admin.Id, template.Id));

        await using var readCtx = _fixture.CreateContext();
        Assert.True(await readCtx.Emails.AnyAsync(e => e.TemplateId == template.Id, TestContext.Current.CancellationToken));
        await _store.DidNotReceive().DeleteIfExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DuplicateAsync_SentEmail_CopiesContentAndRecipientsAsTheCallersDraftWithoutADirectoryLookup()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var member = await _fixture.AddUserAsync("bob@contoso.com");
        var template = await _fixture.AddTemplateAsync(team.Id, admin.Id);
        var source = await _fixture.AddEmailAsync(team.Id, admin.Id, EmailStatuses.Sent);
        var graceId = Guid.NewGuid();
        await using (var setupCtx = _fixture.CreateContext())
        {
            var stored = await setupCtx.Emails.SingleAsync(e => e.Id == source.Id, TestContext.Current.CancellationToken);
            stored.TemplateId = template.Id;
            setupCtx.AddRange(
                new EmailRecipient { EmailId = source.Id, Address = "zed@external.org", RecipientTypeId = RecipientTypes.Cc, Position = 0 },
                new EmailRecipient { EmailId = source.Id, Address = "grace@contoso.com", RecipientTypeId = RecipientTypes.To, Position = 0, UserId = graceId, DisplayName = "Grace Hopper" });
            await setupCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var ctx = _fixture.CreateContext();

        var copy = await DuplicateAsync(ctx, team.Id, source.Id, member.Id);

        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal(EmailStatuses.Draft, copy.Status);
        Assert.Equal(member.Id, copy.AuthorId);
        Assert.Equal(template.Id, copy.TemplateId);
        Assert.Equal(source.Subject, copy.Subject);
        Assert.Equal(source.BodyHtml, copy.BodyHtml);
        Assert.Equal(
            [new EmailRecipientDto("grace@contoso.com", RecipientTypes.To, graceId, "Grace Hopper"), new EmailRecipientDto("zed@external.org", RecipientTypes.Cc, null, null)],
            copy.Recipients);
        await _directory.DidNotReceive().FindByEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>());
        await using var readCtx = _fixture.CreateContext();
        var original = await readCtx.Emails.Include(e => e.Recipients).SingleAsync(e => e.Id == source.Id, TestContext.Current.CancellationToken);
        Assert.Equal(EmailStatuses.Sent, original.StatusId);
        Assert.Equal(2, original.Recipients.Count);
    }

    [Fact]
    public async Task DuplicateAsync_StoredAndFailedAttachments_CopiesOnlyStoredOnesUnderTheCopy()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var stored = NewEmailAttachment(AttachmentStatuses.Uploaded);
        var failed = NewEmailAttachment(AttachmentStatuses.Failed);
        var source = await _fixture.AddEmailAsync(team.Id, admin.Id, EmailStatuses.Draft, stored, failed);
        await using var ctx = _fixture.CreateContext();

        var copy = await DuplicateAsync(ctx, team.Id, source.Id, admin.Id);

        var attachment = Assert.Single(copy.Attachments);
        Assert.NotEqual(stored.Id, attachment.Id);
        Assert.Equal(stored.FileName, attachment.FileName);
        Assert.Equal(AttachmentStatuses.Uploaded, attachment.Status);
        await using var readCtx = _fixture.CreateContext();
        var row = await readCtx.EmailAttachments.SingleAsync(a => a.EmailId == copy.Id, TestContext.Current.CancellationToken);
        Assert.Equal($"{copy.Id:N}/{attachment.Id:N}", row.BlobName);
        await _store.Received(1).CopyAsync(stored.BlobName, row.BlobName, Arg.Any<CancellationToken>());
        await _store.DidNotReceive().CopyAsync(failed.BlobName, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(AttachmentStatuses.Pending)]
    [InlineData(AttachmentStatuses.Uploading)]
    public async Task DuplicateAsync_AttachmentStillUploading_ThrowsNotReadyAndSavesNothing(AttachmentStatuses status)
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var source = await _fixture.AddEmailAsync(team.Id, admin.Id, EmailStatuses.Draft, NewEmailAttachment(status));
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<EmailNotReadyException>(() => DuplicateAsync(ctx, team.Id, source.Id, admin.Id));

        await using var readCtx = _fixture.CreateContext();
        Assert.Equal(1, await readCtx.Emails.CountAsync(TestContext.Current.CancellationToken));
        await _store.DidNotReceive().CopyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DuplicateAsync_AnotherTeamsEmail_ThrowsNotFound()
    {
        var team = await _fixture.AddTeamAsync();
        var otherTeam = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var source = await _fixture.AddEmailAsync(otherTeam.Id, admin.Id);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => DuplicateAsync(ctx, team.Id, source.Id, admin.Id));
    }

    [Fact]
    public async Task DuplicateAsync_SecondCopyFails_DeletesTheFirstCopyAndSavesNoEmail()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var first = NewEmailAttachment(AttachmentStatuses.Uploaded);
        var second = NewEmailAttachment(AttachmentStatuses.Uploaded);
        var source = await _fixture.AddEmailAsync(team.Id, admin.Id, EmailStatuses.Draft, first);

        // Attached later, so it is copied second: copies run oldest first.
        _fixture.Time.Advance(TimeSpan.FromMinutes(1));
        await using (var attachCtx = _fixture.CreateContext())
        {
            second.EmailId = source.Id;
            attachCtx.Add(second);
            await attachCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        List<string> copies = [];
        _store.CopyAsync(first.BlobName, Arg.Do<string>(copies.Add), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        _store.CopyAsync(second.BlobName, Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new BlobNotFoundException(second.BlobName));
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => DuplicateAsync(ctx, team.Id, source.Id, admin.Id));

        var copied = Assert.Single(copies);
        await _store.Received(1).DeleteIfExistsAsync(copied, Arg.Any<CancellationToken>());
        await using var readCtx = _fixture.CreateContext();
        Assert.Equal(1, await readCtx.Emails.CountAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(EmailStatuses.Draft, "Remove it from the email")]
    [InlineData(EmailStatuses.Sent, "cannot be duplicated")]
    public async Task DuplicateAsync_DocumentGoneFromStorage_ThrowsNotFoundWithAdviceTheSourceAllows(EmailStatuses status, string advice)
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var stored = NewEmailAttachment(AttachmentStatuses.Uploaded);
        var source = await _fixture.AddEmailAsync(team.Id, admin.Id, status, stored);
        _store.CopyAsync(stored.BlobName, Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new BlobNotFoundException(stored.BlobName));
        await using var ctx = _fixture.CreateContext();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() => DuplicateAsync(ctx, team.Id, source.Id, admin.Id));

        Assert.Contains(advice, ex.Message, StringComparison.Ordinal);
        if (status == EmailStatuses.Sent)
        {
            Assert.DoesNotContain("Remove it", ex.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task DuplicateAsync_SaveFailsAfterItsCommit_KeepsTheCopiesOfTheSavedEmail()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var source = await _fixture.AddEmailAsync(team.Id, admin.Id, EmailStatuses.Draft, NewEmailAttachment(AttachmentStatuses.Uploaded));
        await using var ctx = _fixture.CreateContext(new FailAfterCommit());

        await Assert.ThrowsAsync<TimeoutException>(() => DuplicateAsync(ctx, team.Id, source.Id, admin.Id));

        await using var readCtx = _fixture.CreateContext();
        Assert.Equal(2, await readCtx.Emails.CountAsync(TestContext.Current.CancellationToken));
        await _store.DidNotReceive().DeleteIfExistsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    public void Dispose() => _fixture.Dispose();

    // Stands in for a commit whose acknowledgement never arrived: the rows are saved, but the call still fails.
    private sealed class FailAfterCommit : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) =>
            throw new TimeoutException("The commit was not acknowledged.");
    }

    #region Private methods

    private static EmailTemplateAttachment NewAttachment(AttachmentStatuses status)
    {
        var id = Guid.CreateVersion7();

        return new EmailTemplateAttachment
        {
            Id = id,
            FileName = $"{id:N}.pdf",
            DocumentTypeId = DocumentTypes.Pdf,
            ContentType = "application/pdf",
            SizeBytes = TestFiles.Pdf.Length,
            BlobName = $"templates/{id:N}",
            StatusId = status,
        };
    }

    private static EmailAttachment NewEmailAttachment(AttachmentStatuses status)
    {
        var id = Guid.CreateVersion7();

        return new EmailAttachment
        {
            Id = id,
            FileName = $"{id:N}.pdf",
            DocumentTypeId = DocumentTypes.Pdf,
            ContentType = "application/pdf",
            SizeBytes = TestFiles.Pdf.Length,
            BlobName = $"emails/{id:N}",
            StatusId = status,
        };
    }

    private Task<EmailDto> DuplicateAsync(AndesMailerDbContext ctx, Guid teamId, Guid id, Guid authorId) =>
        CreateService(ctx).DuplicateAsync(teamId, id, authorId, TestContext.Current.CancellationToken);

    private Task<EmailDto> CreateFromTemplateAsync(AndesMailerDbContext ctx, Guid teamId, Guid authorId, Guid templateId) =>
        CreateService(ctx).CreateFromTemplateAsync(teamId, authorId, new CreateEmailFromTemplateActionDto(templateId), TestContext.Current.CancellationToken);

    private async Task AddRecipientsAsync(Guid templateId, params (string Address, RecipientTypes Type)[] recipients)
    {
        await using var ctx = _fixture.CreateContext();
        ctx.AddRange(recipients.Select((r, position) => new EmailTemplateRecipient { TemplateId = templateId, Address = r.Address, RecipientTypeId = r.Type, Position = position }));
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private EmailService CreateService(AndesMailerDbContext ctx) => new(ctx, _directory, _store, NullLogger<EmailService>.Instance);

    private async Task<EmailDto> CreateDraftAsync(Guid teamId, Guid authorId, params EmailRecipientActionDto[] recipients)
    {
        await using var ctx = _fixture.CreateContext();

        return await CreateService(ctx).CreateAsync(teamId, authorId, new CreateEmailActionDto("First", "<p>One</p>", recipients), TestContext.Current.CancellationToken);
    }

    #endregion
}
