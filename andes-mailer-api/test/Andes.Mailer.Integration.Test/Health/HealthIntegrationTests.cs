using System.Net;

using Andes.Mailer.Integration.Test.TestInfrastructure;

namespace Andes.Mailer.Integration.Test.Health;

public sealed class HealthIntegrationTests(SqlServerFixture fixture)
{
    private const string LivePath = "/health/live";
    private const string ReadyPath = "/health/ready";

    // Port 1 refuses the connection, so the probe fails without waiting on a network timeout.
    private const string UnreachableStore = "Server=127.0.0.1,1;Database=Unreachable;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task GetReady_StoreReachable_Returns200()
    {
        var status = await GetStatusAsync(_fixture.ConnectionString, ReadyPath);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task GetReady_StoreUnreachable_Returns503()
    {
        var status = await GetStatusAsync(UnreachableStore, ReadyPath);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
    }

    [Fact]
    public async Task GetLive_StoreUnreachable_Returns200()
    {
        var status = await GetStatusAsync(UnreachableStore, LivePath);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    #region Private methods

    private static async Task<HttpStatusCode> GetStatusAsync(string connectionString, string path)
    {
        await using var factory = new AndesMailerApiFactory(connectionString);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        return response.StatusCode;
    }

    #endregion
}
