using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
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

        var found = await CreateService(graph).FindByEmailsAsync(
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

        var found = await CreateService(graph).FindByEmailsAsync([], TestContext.Current.CancellationToken);

        Assert.Empty(found);
        Assert.Null(handler.LastRequestUri);
    }

    [Fact]
    public async Task SearchAsync_Query_SendsSearchClausesWithEventualConsistency()
    {
        using var handler = new StubHttpMessageHandler(Accounts());

        await SearchAsync(handler, "  Ana   Roj ", 5);

        var query = Uri.UnescapeDataString(handler.LastRequestUri!.Query);
        Assert.Contains("$search=\"displayName:ana roj\" OR \"givenName:ana roj\" OR \"surname:ana roj\" OR \"mail:ana roj\" OR \"userPrincipalName:ana roj\"", query, StringComparison.Ordinal);
        Assert.Contains("$top=5", query, StringComparison.Ordinal);
        Assert.Contains("$count=true", query, StringComparison.Ordinal);
        Assert.Contains("$orderby=displayName", query, StringComparison.Ordinal);
        Assert.Equal("eventual", handler.LastRequestHeaders["ConsistencyLevel"]);
    }

    [Fact]
    public async Task SearchAsync_QueryWithQuoteAndBackslash_EscapesSearchLiteral()
    {
        using var handler = new StubHttpMessageHandler(Accounts());

        await SearchAsync(handler, @"a""b\c", 5);

        var query = Uri.UnescapeDataString(handler.LastRequestUri!.Query);
        Assert.Contains(@"""displayName:a\""b\\c""", query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SearchAsync_Accounts_MapsMailOrSignInNameAndSkipsAccountsWithoutAddress()
    {
        var guestId = Guid.NewGuid();
        using var handler = new StubHttpMessageHandler(Accounts(
            $$"""{ "id": "{{_accountId}}", "givenName": "Ana", "surname": "Rojas", "mail": "Ana.Rojas@Contoso.com", "userPrincipalName": "ana@contoso.onmicrosoft.com" }""",
            $$"""{ "id": "{{guestId}}", "displayName": "Ana Guest", "mail": null, "userPrincipalName": "ana@contoso.com" }""",
            $$"""{ "id": "{{Guid.NewGuid()}}", "displayName": "No Address" }"""));

        var accounts = await SearchAsync(handler, "ana", 10);

        Assert.Equal(
            [new DirectoryUser(_accountId, "Ana", "Rojas", "ana.rojas@contoso.com"), new DirectoryUser(guestId, "Ana Guest", string.Empty, "ana@contoso.com")],
            accounts);
    }

    [Fact]
    public async Task SearchAsync_SameQueryTwice_CallsGraphOnce()
    {
        using var handler = new StubHttpMessageHandler(Accounts($$"""{ "id": "{{_accountId}}", "givenName": "Ana", "mail": "ana@contoso.com" }"""));
        using var http = new HttpClient(handler, disposeHandler: false);
        using var graph = new GraphServiceClient(http, new AnonymousAuthenticationProvider());
        var service = CreateService(graph);

        await service.SearchAsync("Ana", 10, TestContext.Current.CancellationToken);
        var second = await service.SearchAsync(" ana ", 10, TestContext.Current.CancellationToken);

        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(_accountId, Assert.Single(second).Id);
    }

    #region Private methods

    private static string Accounts(params string[] accounts) => $$"""{ "value": [{{string.Join(',', accounts)}}] }""";

    private static async Task<DirectoryUser?> FindAsync(StubHttpMessageHandler handler, string email)
    {
        using var http = new HttpClient(handler, disposeHandler: false);
        using var graph = new GraphServiceClient(http, new AnonymousAuthenticationProvider());

        return await CreateService(graph).FindByEmailAsync(email, TestContext.Current.CancellationToken);
    }

    // A fresh provider per service, so no cached search leaks from one test into another.
    private static DirectoryUserService CreateService(GraphServiceClient graph) =>
        new(graph, new ServiceCollection().AddHybridCache().Services.BuildServiceProvider().GetRequiredService<HybridCache>());

    private static async Task<IReadOnlyList<DirectoryUser>> SearchAsync(StubHttpMessageHandler handler, string query, int limit)
    {
        using var http = new HttpClient(handler, disposeHandler: false);
        using var graph = new GraphServiceClient(http, new AnonymousAuthenticationProvider());

        return await CreateService(graph).SearchAsync(query, limit, TestContext.Current.CancellationToken);
    }

    #endregion
}
