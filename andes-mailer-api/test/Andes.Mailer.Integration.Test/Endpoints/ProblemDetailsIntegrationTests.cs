using System.Net;
using System.Text.Json;

using Andes.Mailer.Integration.Test.TestInfrastructure;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Andes.Mailer.Integration.Test.Endpoints;

public sealed class ProblemDetailsIntegrationTests(SqlServerFixture fixture)
{
    private const string ProblemJson = "application/problem+json";
    private const string NotFoundType = "https://tools.ietf.org/html/rfc9110#section-15.5.5";
    private const string UnexpectedType = "https://tools.ietf.org/html/rfc9110#section-15.6.1";
    private const string UnknownPath = "/unknown";

    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Get_UnknownRoute_ReturnsNotFoundProblem()
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(UnknownPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        using var body = await ReadBodyAsync(response);
        Assert.Equal(NotFoundType, body.RootElement.GetProperty("type").GetString());
        Assert.Equal(UnknownPath, body.RootElement.GetProperty("instance").GetString());
        Assert.False(string.IsNullOrEmpty(body.RootElement.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Get_UnhandledException_ReturnsUnexpectedProblemWithoutDetail()
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString);
        await using var throwingFactory = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<IStartupFilter, ThrowingEndpointStartupFilter>()));
        using var client = throwingFactory.CreateClient();

        using var response = await client.GetAsync(new Uri(ThrowingEndpointStartupFilter.Path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        using var body = await ReadBodyAsync(response);
        Assert.Equal(UnexpectedType, body.RootElement.GetProperty("type").GetString());
        Assert.Equal(ThrowingEndpointStartupFilter.Path, body.RootElement.GetProperty("instance").GetString());
        Assert.False(body.RootElement.TryGetProperty("detail", out _));
        Assert.DoesNotContain(ThrowingEndpointStartupFilter.ExceptionMessage, body.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    #region Private methods

    private static async Task<JsonDocument> ReadBodyAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

    #endregion
}
