using System.Security.Cryptography;

using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;
using Andes.Mailer.Dto.Actions.Emails.Templates;
using Andes.Mailer.Dto.Emails.Templates;
using Andes.Mailer.Entity.Emails.Templates;
using Andes.Mailer.Repository.Blob;
using Andes.Mailer.Repository.Blob.Exceptions;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails.Exceptions;
using Andes.Mailer.Service.Emails.Templates;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Andes.Mailer.Unit.Test.Service.Emails.Templates;

public sealed class EmailTemplateServiceTests : IDisposable
{
    private readonly MailerDbFixture _fixture = new();
    private readonly IAttachmentStore _store = Substitute.For<IAttachmentStore>();

    [Fact]
    public async Task CreateAsync_Template_StoresTrimmedNameSanitizedBodyAndRecipientsInOrder()
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var action = new CreateEmailTemplateActionDto(
            " Monthly report ",
            " Report ",
            "<p>Hi</p><script>alert(1)</script>",
            [new("zed@external.org", RecipientTypes.Cc), new(" GRACE@contoso.com ", RecipientTypes.To)]);
        await using var ctx = _fixture.CreateContext();

        var template = await CreateService(ctx).CreateAsync(team.Id, author.Id, action, TestContext.Current.CancellationToken);

        Assert.Equal("Monthly report", template.Name);
        Assert.Equal("Report", template.Subject);
        Assert.Equal("<p>Hi</p>", template.BodyHtml);
        Assert.Equal(author.Id, template.AuthorId);
        Assert.Equal(
            [new EmailTemplateRecipientDto("grace@contoso.com", RecipientTypes.To), new EmailTemplateRecipientDto("zed@external.org", RecipientTypes.Cc)],
            template.Recipients);
        await using var readCtx = _fixture.CreateContext();
        var readBack = await CreateService(readCtx).GetAsync(team.Id, template.Id, TestContext.Current.CancellationToken);
        Assert.Equal(template.Name, readBack.Name);
        Assert.Equal(template.BodyHtml, readBack.BodyHtml);
        Assert.Equal(template.Recipients, readBack.Recipients);
    }

    [Fact]
    public async Task CreateAsync_NameOfAnActiveTemplate_ThrowsConflict()
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.AddTemplateAsync(team.Id, author.Id, "Welcome");
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(ctx).CreateAsync(team.Id, author.Id, new CreateEmailTemplateActionDto(" Welcome ", string.Empty, string.Empty, []), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_NameOfAnotherTeamsOrADeactivatedTemplate_Succeeds()
    {
        var team = await _fixture.AddTeamAsync();
        var otherTeam = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.AddTemplateAsync(otherTeam.Id, author.Id, "Welcome");
        var retired = await _fixture.AddTemplateAsync(team.Id, author.Id, "Welcome");
        await using (var deleteCtx = _fixture.CreateContext())
        {
            await CreateService(deleteCtx).DeleteAsync(team.Id, retired.Id, TestContext.Current.CancellationToken);
        }

        await using var ctx = _fixture.CreateContext();
        var template = await CreateService(ctx).CreateAsync(team.Id, author.Id, new CreateEmailTemplateActionDto("Welcome", string.Empty, string.Empty, []), TestContext.Current.CancellationToken);

        Assert.Equal("Welcome", template.Name);
    }

    [Fact]
    public async Task UpdateAsync_Template_ReplacesContentAndKeepsStayingRecipients()
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var created = await CreateTemplateAsync(team.Id, author.Id, new("amy@external.org", RecipientTypes.To), new("bob@external.org", RecipientTypes.Cc));
        Guid amyRowId;
        await using (var readCtx = _fixture.CreateContext())
        {
            amyRowId = (await readCtx.EmailTemplateRecipients.SingleAsync(r => r.TemplateId == created.Id && r.Address == "amy@external.org", TestContext.Current.CancellationToken)).Id;
        }

        await using var ctx = _fixture.CreateContext();
        var updated = await CreateService(ctx).UpdateAsync(
            team.Id,
            created.Id,
            new UpdateEmailTemplateActionDto("Renamed", "Second", "<p>Two</p>", [new("cat@external.org", RecipientTypes.To), new("amy@external.org", RecipientTypes.Bcc)], created.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal("Renamed", updated.Name);
        Assert.Equal("Second", updated.Subject);
        Assert.Equal(
            [new EmailTemplateRecipientDto("cat@external.org", RecipientTypes.To), new EmailTemplateRecipientDto("amy@external.org", RecipientTypes.Bcc)],
            updated.Recipients);
        await using var checkCtx = _fixture.CreateContext();
        var rows = await checkCtx.EmailTemplateRecipients.Where(r => r.TemplateId == created.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.Id == amyRowId && r.RecipientTypeId == RecipientTypes.Bcc);
    }

    [Fact]
    public async Task UpdateAsync_StaleVersion_ThrowsConflict()
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var created = await CreateTemplateAsync(team.Id, author.Id);
        await using var ctx = _fixture.CreateContext();
        var stale = Convert.ToBase64String(new byte[8]);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(ctx).UpdateAsync(team.Id, created.Id, new UpdateEmailTemplateActionDto("x", string.Empty, string.Empty, [], stale), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_NameOfAnotherActiveTemplate_ThrowsConflict()
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.AddTemplateAsync(team.Id, author.Id, "Welcome");
        var created = await CreateTemplateAsync(team.Id, author.Id);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(ctx).UpdateAsync(team.Id, created.Id, new UpdateEmailTemplateActionDto("Welcome", string.Empty, string.Empty, [], created.Version), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_KeepingItsOwnName_Succeeds()
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var created = await CreateTemplateAsync(team.Id, author.Id);
        await using var ctx = _fixture.CreateContext();

        var updated = await CreateService(ctx).UpdateAsync(team.Id, created.Id, new UpdateEmailTemplateActionDto(created.Name, "New subject", string.Empty, [], created.Version), TestContext.Current.CancellationToken);

        Assert.Equal("New subject", updated.Subject);
    }

    [Fact]
    public async Task GetAsync_AnotherTeamsTemplate_ThrowsNotFound()
    {
        var team = await _fixture.AddTeamAsync();
        var otherTeam = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var template = await _fixture.AddTemplateAsync(team.Id, author.Id);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(ctx).GetAsync(otherTeam.Id, template.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_Template_DeactivatesItAndHidesItsRecipients()
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var created = await CreateTemplateAsync(team.Id, author.Id, new EmailRecipientActionDto("amy@external.org", RecipientTypes.To));
        await using (var ctx = _fixture.CreateContext())
        {
            await CreateService(ctx).DeleteAsync(team.Id, created.Id, TestContext.Current.CancellationToken);
        }

        await using var readCtx = _fixture.CreateContext();
        var row = await readCtx.EmailTemplates.IgnoreQueryFilters().SingleAsync(t => t.Id == created.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(row.DateDeactivated);
        Assert.Empty(await readCtx.EmailTemplateRecipients.Where(r => r.TemplateId == created.Id).ToListAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(readCtx).GetAsync(team.Id, created.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListAsync_TeamTemplates_ReturnsOnlyTheTeamsOrderedByNameAndPaged()
    {
        var team = await _fixture.AddTeamAsync();
        var otherTeam = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.AddTemplateAsync(team.Id, author.Id, "Charlie");
        await _fixture.AddTemplateAsync(team.Id, author.Id, "Alpha");
        await _fixture.AddTemplateAsync(team.Id, author.Id, "Bravo");
        await _fixture.AddTemplateAsync(otherTeam.Id, author.Id, "Aardvark");
        await using var ctx = _fixture.CreateContext();

        var page = await CreateService(ctx).ListAsync(team.Id, new ListEmailTemplatesActionDto(Page: 1, PageSize: 2), TestContext.Current.CancellationToken);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(["Alpha", "Bravo"], page.Items.Select(t => t.Name));
    }

    [Fact]
    public async Task CreateAsync_NameTakenBetweenTheCheckAndTheSave_ThrowsConflict()
    {
        var team = await _fixture.AddTeamAsync();
        var author = await _fixture.AddUserAsync("ada@contoso.com");
        var racer = new SameNameRacer(_fixture, team.Id, author.Id, "Welcome");
        await using var ctx = _fixture.CreateContext(racer);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(ctx).CreateAsync(team.Id, author.Id, new CreateEmailTemplateActionDto("Welcome", string.Empty, string.Empty, []), TestContext.Current.CancellationToken));

        Assert.True(racer.Raced);
    }

    [Fact]
    public async Task DuplicateAsync_NoName_CopiesContentAndRecipientsAndNamesItAfterTheSource()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var member = await _fixture.AddUserAsync("bob@contoso.com");
        var source = await _fixture.AddTemplateAsync(team.Id, admin.Id, "Welcome");
        await using (var setupCtx = _fixture.CreateContext())
        {
            setupCtx.AddRange(
                new EmailTemplateRecipient { TemplateId = source.Id, Address = "zed@external.org", RecipientTypeId = RecipientTypes.Cc, Position = 0 },
                new EmailTemplateRecipient { TemplateId = source.Id, Address = "grace@contoso.com", RecipientTypeId = RecipientTypes.To, Position = 0 });
            await setupCtx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var ctx = _fixture.CreateContext();

        var copy = await DuplicateAsync(ctx, team.Id, source.Id, member.Id, null);

        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal("Welcome (copy)", copy.Name);
        Assert.Equal(member.Id, copy.AuthorId);
        Assert.Equal(source.Subject, copy.Subject);
        Assert.Equal(source.BodyHtml, copy.BodyHtml);
        Assert.Equal(
            [new EmailTemplateRecipientDto("grace@contoso.com", RecipientTypes.To), new EmailTemplateRecipientDto("zed@external.org", RecipientTypes.Cc)],
            copy.Recipients);
        await using var readCtx = _fixture.CreateContext();
        var readBack = await CreateService(readCtx).GetAsync(team.Id, copy.Id, TestContext.Current.CancellationToken);
        Assert.Equal(copy.Recipients, readBack.Recipients);
    }

    [Fact]
    public async Task DuplicateAsync_CopyNamesTaken_TakesTheNextFreeNumberIgnoringCase()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var source = await _fixture.AddTemplateAsync(team.Id, admin.Id, "Welcome");
        await _fixture.AddTemplateAsync(team.Id, admin.Id, "Welcome (copy)");
        await _fixture.AddTemplateAsync(team.Id, admin.Id, "WELCOME (COPY 2)");
        await using var ctx = _fixture.CreateContext();

        var copy = await DuplicateAsync(ctx, team.Id, source.Id, admin.Id, null);

        Assert.Equal("Welcome (copy 3)", copy.Name);
    }

    [Fact]
    public async Task DuplicateAsync_NameAtTheLimit_CutsItSoTheSuffixFits()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var name = new string('a', EmailTemplateLimits.NameMaxLength);
        var source = await _fixture.AddTemplateAsync(team.Id, admin.Id, name);
        await using var ctx = _fixture.CreateContext();

        var copy = await DuplicateAsync(ctx, team.Id, source.Id, admin.Id, null);

        Assert.Equal(EmailTemplateLimits.NameMaxLength, copy.Name.Length);
        Assert.Equal(name[..(EmailTemplateLimits.NameMaxLength - " (copy)".Length)] + " (copy)", copy.Name);
    }

    [Fact]
    public async Task DuplicateAsync_GivenName_UsesItTrimmed()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var source = await _fixture.AddTemplateAsync(team.Id, admin.Id, "Welcome");
        await using var ctx = _fixture.CreateContext();

        var copy = await DuplicateAsync(ctx, team.Id, source.Id, admin.Id, " Welcome back ");

        Assert.Equal("Welcome back", copy.Name);
    }

    [Fact]
    public async Task DuplicateAsync_GivenNameTaken_ThrowsConflictAndCopiesNothing()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var source = await _fixture.AddTemplateAsync(team.Id, admin.Id, "Welcome", NewAttachment(AttachmentStatuses.Uploaded));
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() => DuplicateAsync(ctx, team.Id, source.Id, admin.Id, "Welcome"));

        await _store.DidNotReceive().CopyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DuplicateAsync_StoredAndFailedAttachments_CopiesOnlyStoredOnesUnderTheCopy()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var stored = NewAttachment(AttachmentStatuses.Uploaded);
        var failed = NewAttachment(AttachmentStatuses.Failed);
        var source = await _fixture.AddTemplateAsync(team.Id, admin.Id, null, stored, failed);
        await using var ctx = _fixture.CreateContext();

        var copy = await DuplicateAsync(ctx, team.Id, source.Id, admin.Id, null);

        var attachment = Assert.Single(copy.Attachments);
        Assert.NotEqual(stored.Id, attachment.Id);
        Assert.Equal(stored.FileName, attachment.FileName);
        Assert.Equal(AttachmentStatuses.Uploaded, attachment.Status);
        await using var readCtx = _fixture.CreateContext();
        var row = await readCtx.EmailTemplateAttachments.SingleAsync(a => a.TemplateId == copy.Id, TestContext.Current.CancellationToken);
        Assert.Equal($"templates/{copy.Id:N}/{attachment.Id:N}", row.BlobName);
        Assert.Equal(stored.ContentHash, row.ContentHash);
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
        var source = await _fixture.AddTemplateAsync(team.Id, admin.Id, null, NewAttachment(status));
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<TemplateNotReadyException>(() => DuplicateAsync(ctx, team.Id, source.Id, admin.Id, null));

        await using var readCtx = _fixture.CreateContext();
        Assert.Equal(1, await readCtx.EmailTemplates.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DuplicateAsync_AnotherTeamsTemplate_ThrowsNotFound()
    {
        var team = await _fixture.AddTeamAsync();
        var otherTeam = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var source = await _fixture.AddTemplateAsync(otherTeam.Id, admin.Id);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => DuplicateAsync(ctx, team.Id, source.Id, admin.Id, null));
    }

    [Fact]
    public async Task DuplicateAsync_DocumentGoneFromStorage_DeletesItsDestinationAndSavesNoTemplate()
    {
        var team = await _fixture.AddTeamAsync();
        var admin = await _fixture.AddUserAsync("ada@contoso.com");
        var stored = NewAttachment(AttachmentStatuses.Uploaded);
        var source = await _fixture.AddTemplateAsync(team.Id, admin.Id, null, stored);
        string? destination = null;
        _store.CopyAsync(stored.BlobName, Arg.Do<string>(d => destination = d), Arg.Any<CancellationToken>())
            .ThrowsAsync(new BlobNotFoundException(stored.BlobName));
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => DuplicateAsync(ctx, team.Id, source.Id, admin.Id, null));

        Assert.NotNull(destination);
        await _store.Received(1).DeleteIfExistsAsync(destination, Arg.Any<CancellationToken>());
        await using var readCtx = _fixture.CreateContext();
        Assert.Equal(1, await readCtx.EmailTemplates.CountAsync(TestContext.Current.CancellationToken));
    }

    public void Dispose() => _fixture.Dispose();

    // Saves a same-name template through another context just before the service's own save, after its availability check passed.
    private sealed class SameNameRacer(MailerDbFixture fixture, Guid teamId, Guid authorId, string name) : SaveChangesInterceptor
    {
        private readonly MailerDbFixture _fixture = fixture;
        private readonly Guid _teamId = teamId;
        private readonly Guid _authorId = authorId;
        private readonly string _name = name;

        public bool Raced { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!Raced)
            {
                Raced = true;
                await _fixture.AddTemplateAsync(_teamId, _authorId, _name);
            }

            return result;
        }
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
            ContentHash = SHA256.HashData(TestFiles.Pdf),
        };
    }

    private EmailTemplateService CreateService(AndesMailerDbContext ctx) => new(ctx, _store, NullLogger<EmailTemplateService>.Instance);

    private Task<EmailTemplateDto> DuplicateAsync(AndesMailerDbContext ctx, Guid teamId, Guid id, Guid authorId, string? name) =>
        CreateService(ctx).DuplicateAsync(teamId, id, authorId, new DuplicateEmailTemplateActionDto(name), TestContext.Current.CancellationToken);

    private async Task<EmailTemplateDto> CreateTemplateAsync(Guid teamId, Guid authorId, params EmailRecipientActionDto[] recipients)
    {
        await using var ctx = _fixture.CreateContext();

        return await CreateService(ctx).CreateAsync(teamId, authorId, new CreateEmailTemplateActionDto($"Template {Guid.NewGuid():N}", "First", "<p>One</p>", recipients), TestContext.Current.CancellationToken);
    }

    #endregion
}
