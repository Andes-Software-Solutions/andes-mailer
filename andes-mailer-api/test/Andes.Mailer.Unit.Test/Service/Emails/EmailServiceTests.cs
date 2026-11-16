using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails;
using Andes.Mailer.Service.Emails.Exceptions;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

namespace Andes.Mailer.Unit.Test.Service.Emails;

public sealed class EmailServiceTests : IDisposable
{
    private readonly MailerDbFixture _fixture = new();
    private readonly IDirectoryUserService _directory = Substitute.For<IDirectoryUserService>();

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

    public void Dispose() => _fixture.Dispose();

    #region Private methods

    private EmailService CreateService(AndesMailerDbContext ctx) => new(ctx, _directory);

    private async Task<EmailDto> CreateDraftAsync(Guid teamId, Guid authorId, params EmailRecipientActionDto[] recipients)
    {
        await using var ctx = _fixture.CreateContext();

        return await CreateService(ctx).CreateAsync(teamId, authorId, new CreateEmailActionDto("First", "<p>One</p>", recipients), TestContext.Current.CancellationToken);
    }

    #endregion
}
