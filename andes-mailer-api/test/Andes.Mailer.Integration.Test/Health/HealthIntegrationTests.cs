using System.Net;

using Andes.Mailer.Integration.Test.TestInfrastructure;

namespace Andes.Mailer.Integration.Test.Health;

public sealed class HealthIntegrationTests(SqlServerFixture fixture, AzuriteFixture azurite)
{
    private const string LivePath = "/health/live";
    private const string ReadyPath = "/health/ready";

    // Port 1 refuses the connection, so the probe fails without waiting on a network timeout.
    private const string UnreachableStore = "Server=127.0.0.1,1;Database=Unreachable;Integrated Security=True;Connect Timeout=1;TrustServerCertificate=True";

    // Azurite's well-known development account, pointed at a port that refuses the connection.
    private const string UnreachableBlobStore =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "BlobEndpoint=http://127.0.0.1:1/devstoreaccount1;";

    private readonly SqlServerFixture _fixture = fixture;
    private readonly AzuriteFixture _azurite = azurite;

    [Fact]
    public async Task GetReady_StoresReachable_Returns200()
    {
        var status = await GetStatusAsync(_fixture.ConnectionString, _azurite.ConnectionString, ReadyPath);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    [Fact]
    public async Task GetReady_StoreUnreachable_Returns503()
    {
        var status = await GetStatusAsync(UnreachableStore, _azurite.ConnectionString, ReadyPath);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
    }

    [Fact]
    public async Task GetReady_BlobStoreUnreachable_Returns503()
    {
        var status = await GetStatusAsync(_fixture.ConnectionString, UnreachableBlobStore, ReadyPath);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
    }

    [Fact]
    public async Task GetLive_StoreUnreachable_Returns200()
    {
        var status = await GetStatusAsync(UnreachableStore, _azurite.ConnectionString, LivePath);

        Assert.Equal(HttpStatusCode.OK, status);
    }

    #region Private methods

    private static async Task<HttpStatusCode> GetStatusAsync(string connectionString, string blobConnectionString, string path)
    {
        await using var factory = new AndesMailerApiFactory(connectionString, blobConnectionString);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        return response.StatusCode;
    }

    #endregion
}
