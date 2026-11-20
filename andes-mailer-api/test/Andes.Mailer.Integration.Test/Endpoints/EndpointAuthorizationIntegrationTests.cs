using System.Net;

using Andes.Mailer.Integration.Test.TestInfrastructure;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace Andes.Mailer.Integration.Test.Endpoints;

/// <summary>
/// Guards the rule that every API endpoint needs a token: a new endpoint that opts out, or a group that forgets
/// <c>RequireAuthorization()</c>, fails here instead of shipping.
/// </summary>
public sealed class EndpointAuthorizationIntegrationTests(SqlServerFixture fixture, AzuriteFixture azurite)
{
    private const string ApiPrefix = "/api/";

    // Orchestrator probes carry no token; the Development-only OpenAPI document and /scalar are not mapped under Testing.
    private static readonly string[] _anonymousRoutes = ["/health/live", "/health/ready"];

    private readonly SqlServerFixture _fixture = fixture;
    private readonly AzuriteFixture _azurite = azurite;

    [Fact]
    public async Task MappedEndpoints_Default_OnlyHealthProbesAllowAnonymous()
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString, _azurite.ConnectionString);

        var anonymous = GetRouteEndpoints(factory)
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText)
            .Order(StringComparer.Ordinal);

        Assert.Equal(_anonymousRoutes, anonymous);
    }

    [Fact]
    public async Task ApiEndpoints_Default_RequireAuthorization()
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString, _azurite.ConnectionString);

        var api = GetApiEndpoints(factory);
        var unprotected = api
            .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count == 0)
            .Select(endpoint => endpoint.RoutePattern.RawText);

        Assert.NotEmpty(api);
        Assert.Empty(unprotected);
    }

    [Fact]
    public async Task ApiEndpoints_NoToken_ReturnUnauthorized()
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString, _azurite.ConnectionString);
        using var client = factory.CreateClient();

        List<string> failures = [];
        foreach (var endpoint in GetApiEndpoints(factory))
        {
            var path = BuildPath(endpoint.RoutePattern);
            foreach (var method in endpoint.Metadata.GetRequiredMetadata<HttpMethodMetadata>().HttpMethods)
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), new Uri(path, UriKind.Relative));
                using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
                if (response.StatusCode != HttpStatusCode.Unauthorized)
                {
                    failures.Add($"{method} {path}: {(int)response.StatusCode}");
                }
            }
        }

        Assert.Empty(failures);
    }

    #region Private methods

    private static List<RouteEndpoint> GetRouteEndpoints(AndesMailerApiFactory factory) =>
        [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];

    private static List<RouteEndpoint> GetApiEndpoints(AndesMailerApiFactory factory) =>
        [.. GetRouteEndpoints(factory).Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith(ApiPrefix, StringComparison.Ordinal) == true)];

    // Values satisfy each route constraint, so the request reaches the endpoint; an unmatched route would also answer 401 through the fallback policy.
    private static string BuildPath(RoutePattern pattern) =>
        "/" + string.Join('/', pattern.PathSegments.Select(segment => string.Concat(segment.Parts.Select(part => part switch
        {
            RoutePatternLiteralPart literal => literal.Content,
            RoutePatternSeparatorPart separator => separator.Content,
            RoutePatternParameterPart parameter => ParameterValue(pattern, parameter),
            _ => throw new NotSupportedException($"Unexpected route part in {pattern.RawText}."),
        }))));

    private static string ParameterValue(RoutePattern pattern, RoutePatternParameterPart parameter) =>
        parameter.ParameterPolicies.Select(policy => policy.Content).SingleOrDefault() switch
        {
            "guid" => Guid.NewGuid().ToString(),
            "int" or "long" => "1",
            null => "value",
            var constraint => throw new NotSupportedException($"Add a value for the '{constraint}' constraint in {pattern.RawText}."),
        };

    #endregion
}
