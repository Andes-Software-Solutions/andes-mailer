using System.Net;

using Andes.Mailer.Common.Constants;
using Andes.Mailer.Integration.Test.TestInfrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;

namespace Andes.Mailer.Integration.Test.Middleware;

public sealed class RequestLoggingIntegrationTests(SqlServerFixture fixture, AzuriteFixture azurite)
{
    private const string RequestLoggingCategory = "Andes.Mailer.Api.Middleware.RequestLoggingMiddleware";

    private readonly SqlServerFixture _fixture = fixture;
    private readonly AzuriteFixture _azurite = azurite;

    [Fact]
    public async Task Get_SignedInCaller_LogsRequestWithCallerRouteAndTrace()
    {
        var userId = await TestCallers.SeedAsync(_fixture);
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, userId);

        using var response = await client.GetAsync(new Uri("/api/users/me", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var record = await WaitForRequestRecordAsync(factory);
        Assert.Equal(userId.ToString(), record.GetStructuredStateValue("UserId"));
        Assert.Equal("/api/users/me", record.GetStructuredStateValue("RouteTemplate"));
        Assert.Equal("200", record.GetStructuredStateValue("StatusCode"));
        Assert.Matches("^[0-9a-f]{32}$", record.GetStructuredStateValue("TraceId"));
    }

    [Fact]
    public async Task Get_TeamOutsideCallersTeams_LogsTheNotFoundTheExceptionHandlerWrote()
    {
        var userId = await TestCallers.SeedAsync(_fixture);
        var teamId = Guid.NewGuid();
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, userId);

        using var response = await client.GetAsync(new Uri($"/api/teams/{teamId}/emails", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var record = await WaitForRequestRecordAsync(factory);
        Assert.Equal("404", record.GetStructuredStateValue("StatusCode"));
        Assert.Equal(teamId.ToString(), record.GetStructuredStateValue("TeamId"));
        Assert.StartsWith("/api/teams/{teamId:guid}/emails", record.GetStructuredStateValue("RouteTemplate"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_HealthProbe_IsNotLogged()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(RequestRecords(factory));
    }

    #region Private methods

    private AndesMailerApiFactory CreateFactory() => new(
        _fixture.ConnectionString,
        _azurite.ConnectionString,
        useTestAuthentication: true,
        configureTestServices: services => services.AddFakeLogging());

    private static HttpClient CreateClient(AndesMailerApiFactory factory, Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopesHeader, ApiScopes.AccessAsUser);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());

        return client;
    }

    // The line is written once the pipeline unwinds, which can be just after the test server hands the response back.
    private static async Task<FakeLogRecord> WaitForRequestRecordAsync(AndesMailerApiFactory factory)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (!RequestRecords(factory).Any())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
        }

        return Assert.Single(RequestRecords(factory));
    }

    private static IEnumerable<FakeLogRecord> RequestRecords(AndesMailerApiFactory factory) =>
        factory.Services.GetFakeLogCollector().GetSnapshot().Where(r => r.Category == RequestLoggingCategory);

    #endregion
}
