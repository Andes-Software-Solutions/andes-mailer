using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.Graph;
using Microsoft.Kiota.Abstractions.Authentication;

namespace Andes.Mailer.Unit.Test.Service.Users;

public sealed class DirectoryUserServiceTests
{
    private static readonly Guid _accountId = Guid.NewGuid();

    [Fact]
    public async Task FindByEmailAsync_AccountWithNames_ReturnsDirectoryUser()
    {
        using var handler = new StubHttpMessageHandler(Accounts($$"""{ "id": "{{_accountId}}", "givenName": "Grace", "surname": "Hopper", "displayName": "Rear Admiral Hopper" }"""));

        var user = await FindAsync(handler, " Grace@Contoso.COM ");

        Assert.NotNull(user);
        Assert.Equal(_accountId, user.Id);
        Assert.Equal("Grace", user.FirstName);
        Assert.Equal("Hopper", user.LastName);
        Assert.Equal("grace@contoso.com", user.Email);
    }

    [Fact]
    public async Task FindByEmailAsync_AccountWithDisplayNameOnly_UsesDisplayNameAndEmptySurname()
    {
        using var handler = new StubHttpMessageHandler(Accounts($$"""{ "id": "{{_accountId}}", "displayName": "Grace Hopper" }"""));

        var user = await FindAsync(handler, "grace@contoso.com");

        Assert.Equal("Grace Hopper", user?.FirstName);
        Assert.Equal(string.Empty, user?.LastName);
    }

    [Fact]
    public async Task FindByEmailAsync_AccountWithoutNames_UsesEmailLocalPart()
    {
        using var handler = new StubHttpMessageHandler(Accounts($$"""{ "id": "{{_accountId}}" }"""));

        var user = await FindAsync(handler, "grace@contoso.com");

        Assert.Equal("grace", user?.FirstName);
    }

    [Fact]
    public async Task FindByEmailAsync_NoMatch_ReturnsNull()
    {
        using var handler = new StubHttpMessageHandler(Accounts());

        Assert.Null(await FindAsync(handler, "nobody@contoso.com"));
    }

    [Fact]
    public async Task FindByEmailAsync_EmailWithQuote_EscapesFilterLiteral()
    {
        using var handler = new StubHttpMessageHandler(Accounts());

        await FindAsync(handler, "o'brien@contoso.com");

        var query = Uri.UnescapeDataString(handler.LastRequestUri!.Query);
        Assert.Contains("mail eq 'o''brien@contoso.com' or userPrincipalName eq 'o''brien@contoso.com'", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindByEmailsAsync_AccountsMatchedByMailOrSignInName_KeysThemByRequestedAddress()
    {
        var externalId = Guid.NewGuid();
        using var handler = new StubHttpMessageHandler(Accounts(
            $$"""{ "id": "{{_accountId}}", "givenName": "Grace", "surname": "Hopper", "mail": "Grace@Contoso.com", "userPrincipalName": "grace@contoso.onmicrosoft.com" }""",
            $$"""{ "id": "{{externalId}}", "displayName": "Amy", "mail": null, "userPrincipalName": "amy@contoso.com" }"""));
        using var http = new HttpClient(handler, disposeHandler: false);
        using var graph = new GraphServiceClient(http, new AnonymousAuthenticationProvider());

        var found = await new DirectoryUserService(graph).FindByEmailsAsync(
            [" GRACE@contoso.com ", "amy@contoso.com", "nobody@external.org"],
            TestContext.Current.CancellationToken);

        Assert.Equal(["amy@contoso.com", "grace@contoso.com"], found.Keys.Order());
        Assert.Equal(new DirectoryUser(_accountId, "Grace", "Hopper", "grace@contoso.com"), found["grace@contoso.com"]);
        Assert.Equal(externalId, found["amy@contoso.com"].Id);
        var query = Uri.UnescapeDataString(handler.LastRequestUri!.Query);
        Assert.Contains("mail in ('grace@contoso.com', 'amy@contoso.com', 'nobody@external.org') or userPrincipalName in (", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FindByEmailsAsync_NoAddresses_MakesNoRequest()
    {
        using var handler = new StubHttpMessageHandler(Accounts());
        using var http = new HttpClient(handler, disposeHandler: false);
        using var graph = new GraphServiceClient(http, new AnonymousAuthenticationProvider());

        var found = await new DirectoryUserService(graph).FindByEmailsAsync([], TestContext.Current.CancellationToken);

        Assert.Empty(found);
        Assert.Null(handler.LastRequestUri);
    }

    #region Private methods

    private static string Accounts(params string[] accounts) => $$"""{ "value": [{{string.Join(',', accounts)}}] }""";

    private static async Task<DirectoryUser?> FindAsync(StubHttpMessageHandler handler, string email)
    {
        using var http = new HttpClient(handler, disposeHandler: false);
        using var graph = new GraphServiceClient(http, new AnonymousAuthenticationProvider());

        return await new DirectoryUserService(graph).FindByEmailAsync(email, TestContext.Current.CancellationToken);
    }

    #endregion
}
