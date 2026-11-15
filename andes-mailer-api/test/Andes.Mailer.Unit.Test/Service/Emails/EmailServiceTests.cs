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
        var sender = await _fixture.AddUserAsync("ada@contoso.com");
        var graceId = Guid.NewGuid();
        _directory.FindByEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, DirectoryUser> { ["grace@contoso.com"] = new(graceId, "Grace", "Hopper", "grace@contoso.com") });
        var action = new CreateEmailActionDto(
            " Hello ",
            "<p>Hi</p><script>alert(1)</script>",
            [new("zed@external.org", RecipientTypes.Cc), new(" GRACE@contoso.com ", RecipientTypes.To), new("amy@external.org", RecipientTypes.To)]);
        await using var ctx = _fixture.CreateContext();

        var created = await CreateService(ctx).CreateAsync(sender.Id, action, TestContext.Current.CancellationToken);
        await using var readCtx = _fixture.CreateContext();
        var reloaded = await CreateService(readCtx).GetAsync(created.Id, sender.Id, TestContext.Current.CancellationToken);

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
    public async Task CreateAsync_CallerNotAUser_ThrowsForbidden()
    {
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateService(ctx).CreateAsync(Guid.NewGuid(), new CreateEmailActionDto("Hi", string.Empty, []), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_AnotherUsersEmail_ThrowsNotFound()
    {
        var owner = await _fixture.AddUserAsync("ada@contoso.com");
        var other = await _fixture.AddUserAsync("bob@contoso.com");
        var email = await _fixture.AddEmailAsync(owner.Id);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(ctx).GetAsync(email.Id, other.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_ChangedRecipients_KeepsStayingRowsAndResolvesOnlyNewAddresses()
    {
        var sender = await _fixture.AddUserAsync("ada@contoso.com");
        var created = await CreateDraftAsync(sender.Id, new EmailRecipientActionDto("amy@external.org", RecipientTypes.To), new EmailRecipientActionDto("bob@external.org", RecipientTypes.Cc));
        _directory.ClearReceivedCalls();
        await using var ctx = _fixture.CreateContext();
        var action = new UpdateEmailActionDto("Second", "<p>Two</p>", [new("bob@external.org", RecipientTypes.To), new("cat@external.org", RecipientTypes.Bcc)], created.Version);

        var updated = await CreateService(ctx).UpdateAsync(created.Id, sender.Id, action, TestContext.Current.CancellationToken);

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
        var sender = await _fixture.AddUserAsync("ada@contoso.com");
        var created = await CreateDraftAsync(sender.Id);
        await using var ctx = _fixture.CreateContext();
        var stale = Convert.ToBase64String(new byte[8]);

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(ctx).UpdateAsync(created.Id, sender.Id, new UpdateEmailActionDto("x", string.Empty, [], stale), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_SentEmail_ThrowsNotEditable()
    {
        var sender = await _fixture.AddUserAsync("ada@contoso.com");
        var email = await _fixture.AddEmailAsync(sender.Id, EmailStatuses.Sent);
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<EmailNotEditableException>(() =>
            CreateService(ctx).UpdateAsync(email.Id, sender.Id, new UpdateEmailActionDto("x", string.Empty, [], Convert.ToBase64String(email.Version)), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_OwnEmail_DeactivatesItAndHidesItsRecipients()
    {
        var sender = await _fixture.AddUserAsync("ada@contoso.com");
        var created = await CreateDraftAsync(sender.Id, new EmailRecipientActionDto("amy@external.org", RecipientTypes.To));
        await using var ctx = _fixture.CreateContext();

        await CreateService(ctx).DeleteAsync(created.Id, sender.Id, TestContext.Current.CancellationToken);

        await using var readCtx = _fixture.CreateContext();
        var row = await readCtx.Emails.IgnoreQueryFilters().SingleAsync(e => e.Id == created.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_fixture.Time.GetUtcNow(), row.DateDeactivated);
        Assert.False(await readCtx.EmailRecipients.AnyAsync(r => r.EmailId == created.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListAsync_OwnEmails_ReturnsOnlyCallersMostRecentFirst()
    {
        var sender = await _fixture.AddUserAsync("ada@contoso.com");
        var other = await _fixture.AddUserAsync("bob@contoso.com");
        var older = await _fixture.AddEmailAsync(sender.Id);
        _fixture.Time.Advance(TimeSpan.FromMinutes(1));
        var newer = await _fixture.AddEmailAsync(sender.Id);
        await _fixture.AddEmailAsync(other.Id);
        await using var ctx = _fixture.CreateContext();

        var page = await CreateService(ctx).ListAsync(sender.Id, new ListEmailsActionDto(), TestContext.Current.CancellationToken);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal([newer.Id, older.Id], page.Items.Select(e => e.Id));
    }

    public void Dispose() => _fixture.Dispose();

    #region Private methods

    private EmailService CreateService(AndesMailerDbContext ctx) => new(ctx, _directory);

    private async Task<EmailDto> CreateDraftAsync(Guid senderId, params EmailRecipientActionDto[] recipients)
    {
        await using var ctx = _fixture.CreateContext();

        return await CreateService(ctx).CreateAsync(senderId, new CreateEmailActionDto("First", "<p>One</p>", recipients), TestContext.Current.CancellationToken);
    }

    #endregion
}
