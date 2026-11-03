using System.Net;
using System.Net.Http.Json;

using Andes.Mailer.Common.Constants;
using Andes.Mailer.Dto.Users;
using Andes.Mailer.Integration.Test.TestInfrastructure;

namespace Andes.Mailer.Integration.Test.Endpoints;

public sealed class UserEndpointsIntegrationTests(SqlServerFixture fixture)
{
    private const string ProblemJson = "application/problem+json";
    private const string MePath = "/api/users/me";
    private const string UnknownPath = "/unknown";

    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task GetMe_NoToken_ReturnsUnauthorizedProblem()
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(MePath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    [Fact]
    public async Task GetUnknownRoute_NoToken_ReturnsUnauthorized()
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(UnknownPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_TokenWithoutRequiredScope_ReturnsForbiddenProblem()
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString, useTestAuthentication: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopesHeader, "other_scope");

        using var response = await client.GetAsync(new Uri(MePath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetMe_TokenWithRequiredScope_ReturnsCurrentUser()
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString, useTestAuthentication: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopesHeader, ApiScopes.AccessAsUser);

        var user = await client.GetFromJsonAsync<CurrentUserDto>(MePath, TestContext.Current.CancellationToken);

        Assert.Equal(new CurrentUserDto(TestAuthHandler.UserId, TestAuthHandler.UserName), user);
    }
}
