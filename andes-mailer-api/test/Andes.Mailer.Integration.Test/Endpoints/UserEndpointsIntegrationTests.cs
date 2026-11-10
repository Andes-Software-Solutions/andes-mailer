using System.Net;
using System.Net.Http.Json;

using Andes.Mailer.Common.Constants;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Users;
using Andes.Mailer.Dto.Users;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Integration.Test.TestInfrastructure;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Andes.Mailer.Integration.Test.Endpoints;

public sealed class UserEndpointsIntegrationTests(SqlServerFixture fixture)
{
    private const string ProblemJson = "application/problem+json";
    private const string UsersPath = "/api/users";
    private const string MePath = "/api/users/me";
    private const string UnknownPath = "/unknown";

    private readonly SqlServerFixture _fixture = fixture;
    private readonly IDirectoryUserService _directory = Substitute.For<IDirectoryUserService>();

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

    [Fact]
    public async Task CreateUser_Administrator_ReturnsCreatedUserFromDirectory()
    {
        var caller = await SeedUserAsync(Roles.Administrator);
        var email = UniqueEmail();
        var account = new DirectoryUser(Guid.NewGuid(), "Grace", "Hopper", email);
        _directory.FindByEmailAsync(email, Arg.Any<CancellationToken>()).Returns(account);
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(UsersPath, new CreateUserActionDto(email.ToUpperInvariant()), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{UsersPath}/{account.Id}", response.Headers.Location?.OriginalString);
        var user = await response.Content.ReadFromJsonAsync<UserDto>(TestContext.Current.CancellationToken);
        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Equal("Grace Hopper", user.FullName);
    }

    [Fact]
    public async Task CreateUser_CallerWithoutPermission_ReturnsForbiddenProblem()
    {
        var caller = await SeedUserAsync(Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(UsersPath, new CreateUserActionDto(UniqueEmail()), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        await _directory.DidNotReceive().FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateUser_InvalidEmail_ReturnsValidationProblem()
    {
        var caller = await SeedUserAsync(Roles.Administrator);
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(UsersPath, new CreateUserActionDto("not-an-email"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Contains(nameof(CreateUserActionDto.Email), problem!.Errors.Keys);
    }

    [Fact]
    public async Task UpdateUser_CurrentVersion_ReturnsUpdatedUser()
    {
        var caller = await SeedUserAsync(Roles.Administrator);
        var target = await SeedUserAsync();
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);
        var current = await GetUserAsync(client, target);

        using var response = await client.PutAsJsonAsync($"{UsersPath}/{target}", new UpdateUserActionDto("Augusta", "King", current.Version), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserDto>(TestContext.Current.CancellationToken);
        Assert.Equal("Augusta King", user!.FullName);
        Assert.NotEqual(current.Version, user.Version);
        Assert.NotNull(user.DateModified);
    }

    [Fact]
    public async Task UpdateUser_StaleVersion_ReturnsConflictProblem()
    {
        var caller = await SeedUserAsync(Roles.Administrator);
        var target = await SeedUserAsync();
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);
        var stale = await GetUserAsync(client, target);
        using var first = await client.PutAsJsonAsync($"{UsersPath}/{target}", new UpdateUserActionDto("Augusta", "King", stale.Version), TestContext.Current.CancellationToken);
        first.EnsureSuccessStatusCode();

        using var response = await client.PutAsJsonAsync($"{UsersPath}/{target}", new UpdateUserActionDto("Ada", "Byron", stale.Version), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UpdateUser_MissingLastName_ReturnsValidationProblem()
    {
        var caller = await SeedUserAsync(Roles.Administrator);
        var target = await SeedUserAsync();
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);
        var current = await GetUserAsync(client, target);

        using var response = await client.PutAsJsonAsync($"{UsersPath}/{target}", new { firstName = "Augusta", version = current.Version }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Contains(nameof(UpdateUserActionDto.LastName), problem!.Errors.Keys);
    }

    [Fact]
    public async Task DeactivateUser_Administrator_ReturnsNoContentAndHidesUser()
    {
        var caller = await SeedUserAsync(Roles.Administrator);
        var target = await SeedUserAsync();
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);

        using var response = await client.DeleteAsync(new Uri($"{UsersPath}/{target}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var get = await client.GetAsync(new Uri($"{UsersPath}/{target}", UriKind.Relative), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task DeactivateUser_Self_ReturnsConflictProblem()
    {
        var caller = await SeedUserAsync(Roles.Administrator);
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);

        using var response = await client.DeleteAsync(new Uri($"{UsersPath}/{caller}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_PageSizeOverLimit_ReturnsValidationProblem()
    {
        var caller = await SeedUserAsync();
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);

        using var response = await client.GetAsync(new Uri($"{UsersPath}?pageSize=1000", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.Contains(nameof(ListUsersActionDto.PageSize), problem!.Errors.Keys);
    }

    [Fact]
    public async Task GetUser_Missing_ReturnsNotFoundProblem()
    {
        var caller = await SeedUserAsync();
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);

        using var response = await client.GetAsync(new Uri($"{UsersPath}/{Guid.NewGuid()}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    #region Private methods

    // Unique per test: the suite shares one database, and an active email must be unique.
    private static string UniqueEmail() => $"user-{Guid.NewGuid():N}@contoso.com";

    private static HttpClient CreateClient(AndesMailerApiFactory factory, Guid callerId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopesHeader, ApiScopes.AccessAsUser);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, callerId.ToString());

        return client;
    }

    private static async Task<UserDto> GetUserAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<UserDto>($"{UsersPath}/{id}", TestContext.Current.CancellationToken))!;

    private AndesMailerApiFactory CreateFactory() => new(
        _fixture.ConnectionString,
        useTestAuthentication: true,
        configureTestServices: services => services.AddScoped(_ => _directory));

    private async Task<Guid> SeedUserAsync(Roles? role = null)
    {
        await using var ctx = _fixture.CreateContext();
        var user = new User { Id = Guid.NewGuid(), FirstName = "Ada", LastName = "Lovelace", Email = UniqueEmail() };
        ctx.Add(user);
        if (role is { } granted)
        {
            ctx.Add(new UserRole { UserId = user.Id, RoleId = granted });
        }

        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return user.Id;
    }

    #endregion
}
