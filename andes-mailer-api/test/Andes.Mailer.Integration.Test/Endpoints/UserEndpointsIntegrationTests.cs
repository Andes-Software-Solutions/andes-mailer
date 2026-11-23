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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Andes.Mailer.Integration.Test.Endpoints;

public sealed class UserEndpointsIntegrationTests(SqlServerFixture fixture, AzuriteFixture azurite)
{
    private const string ProblemJson = "application/problem+json";
    private const string UsersPath = "/api/users";
    private const string MePath = "/api/users/me";
    private const string DirectoryPath = "/api/users/directory";
    private const string UnknownPath = "/unknown";

    private readonly SqlServerFixture _fixture = fixture;
    private readonly AzuriteFixture _azurite = azurite;
    private readonly IDirectoryUserService _directory = Substitute.For<IDirectoryUserService>();

    [Fact]
    public async Task GetMe_NoToken_ReturnsUnauthorizedProblem()
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString, _azurite.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(MePath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
    }

    [Fact]
    public async Task GetUnknownRoute_NoToken_ReturnsUnauthorized()
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString, _azurite.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(UnknownPath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMe_TokenWithoutRequiredScope_ReturnsForbiddenProblem()
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString, _azurite.ConnectionString, useTestAuthentication: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopesHeader, "other_scope");

        using var response = await client.GetAsync(new Uri(MePath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetMe_TokenWithoutObjectId_ReturnsForbiddenProblem()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, Guid.NewGuid());
        client.DefaultRequestHeaders.Add(TestAuthHandler.NoObjectIdHeader, "true");

        using var response = await client.GetAsync(new Uri(MePath, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ProblemJson, response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetMe_UnknownAccount_ReturnsInactiveUserFromToken()
    {
        var caller = Guid.NewGuid();
        var email = UniqueEmail();
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email);

        var user = await GetMeAsync(client);

        Assert.Equal(caller, user.Id);
        Assert.Equal(TestAuthHandler.UserName, user.Name);
        Assert.Equal(email, user.Email);
        Assert.False(user.IsActive);
        Assert.Empty(user.Roles);
        Assert.Empty(user.Permissions);
        Assert.Empty(user.Teams);
    }

    [Fact]
    public async Task GetMe_DeactivatedUser_ReturnsInactiveUser()
    {
        var caller = await SeedUserAsync(Roles.Administrator);
        await using (var ctx = _fixture.CreateContext())
        {
            var row = await ctx.Users.SingleAsync(u => u.Id == caller, TestContext.Current.CancellationToken);
            row.DateDeactivated = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var email = UniqueEmail();
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email);

        var user = await GetMeAsync(client);

        Assert.False(user.IsActive);
        Assert.Equal(email, user.Email);
        Assert.Empty(user.Roles);
        Assert.Empty(user.Permissions);
        Assert.Empty(user.Teams);
    }

    [Fact]
    public async Task GetMe_Administrator_ReturnsEveryPermissionAndEveryActiveTeam()
    {
        var caller = await SeedUserAsync(Roles.Administrator);
        var ownTeam = await TestCallers.SeedTeamAsync(_fixture);
        var otherTeam = await TestCallers.SeedTeamAsync(_fixture);
        await TestCallers.AddMembershipAsync(_fixture, ownTeam, caller, Roles.TeamMember);
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);

        var user = await GetMeAsync(client);

        Assert.True(user.IsActive);
        Assert.Equal("Ada Lovelace", user.Name);
        Assert.NotNull(user.Email);
        Assert.Equal([Roles.Administrator], user.Roles);
        Assert.Equal(Enum.GetValues<Permissions>(), user.Permissions);
        var own = Assert.Single(user.Teams, t => t.Id == ownTeam);
        Assert.Equal(Roles.TeamMember, own.Role);
        Assert.Equal(Enum.GetValues<Permissions>(), own.Permissions);
        var other = Assert.Single(user.Teams, t => t.Id == otherTeam);
        Assert.Null(other.Role);
        Assert.Equal(Enum.GetValues<Permissions>(), other.Permissions);
    }

    [Fact]
    public async Task GetMe_TeamMember_ReturnsOnlyTheirTeamsWithTeamPermissions()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var otherTeam = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamMember);
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);

        var user = await GetMeAsync(client);

        Assert.True(user.IsActive);
        Assert.Empty(user.Roles);
        Assert.Empty(user.Permissions);
        var only = Assert.Single(user.Teams);
        Assert.Equal(team, only.Id);
        Assert.DoesNotContain(user.Teams, t => t.Id == otherTeam);
        Assert.Equal(Roles.TeamMember, only.Role);
        Assert.Equal(
            [Permissions.CanAttachDocuments, Permissions.ViewTeam, Permissions.ComposeTeamEmails, Permissions.DuplicateTeamEmails],
            only.Permissions);
    }

    [Fact]
    public async Task GetMe_TeamAdministrator_ReturnsTeamAdministratorPermissions()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);

        var user = await GetMeAsync(client);

        var only = Assert.Single(user.Teams);
        Assert.Equal(Roles.TeamAdministrator, only.Role);
        Assert.Contains(Permissions.ModifyTeamUser, only.Permissions);
        Assert.Contains(Permissions.SendTeamEmails, only.Permissions);
        Assert.Contains(Permissions.ManageTeamContactGroups, only.Permissions);
        Assert.DoesNotContain(Permissions.ModifyTeam, only.Permissions);
    }

    [Fact]
    public async Task GetMe_MemberOfDeactivatedTeam_OmitsTheTeam()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using (var ctx = _fixture.CreateContext())
        {
            var row = await ctx.Teams.SingleAsync(t => t.Id == team, TestContext.Current.CancellationToken);
            row.DateDeactivated = DateTimeOffset.UtcNow;
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);

        var user = await GetMeAsync(client);

        Assert.True(user.IsActive);
        Assert.Empty(user.Teams);
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
        // A team administrator manages one team's members, never the application's users.
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
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

    [Fact]
    public async Task SearchDirectory_Query_ReturnsMatches()
    {
        var caller = await TestCallers.SeedAsync(_fixture);
        var person = new DirectoryUser(Guid.NewGuid(), "Ana", "Rojas", "ana.rojas@contoso.com");
        _directory.SearchAsync("ana roj", 5, Arg.Any<CancellationToken>()).Returns([person]);
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);

        var found = await client.GetFromJsonAsync<DirectoryUserDto[]>($"{DirectoryPath}?query={Uri.EscapeDataString(" ana roj ")}&limit=5", ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal([new DirectoryUserDto(person.Id, "Ana", "Rojas", "Ana Rojas", "ana.rojas@contoso.com")], found!);
    }

    [Fact]
    public async Task SearchDirectory_QueryTooShort_ReturnsValidationProblem()
    {
        var caller = await TestCallers.SeedAsync(_fixture);
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);

        using var response = await client.GetAsync($"{DirectoryPath}?query=a", TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(nameof(SearchDirectoryActionDto.Query), problem!.Errors.Keys);
        await _directory.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SearchDirectory_AccountNotAUser_ReturnsForbidden()
    {
        await using var factory = CreateFactory();
        using var client = CreateClient(factory, Guid.NewGuid());

        using var response = await client.GetAsync($"{DirectoryPath}?query=ana", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await _directory.DidNotReceiveWithAnyArgs().SearchAsync(default!, default, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SearchDirectory_DeactivatedUser_ReturnsForbidden()
    {
        var caller = await TestCallers.SeedAsync(_fixture);
        await using (var ctx = _fixture.CreateContext())
        {
            await ctx.Users
                .Where(u => u.Id == caller)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.DateDeactivated, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)), TestContext.Current.CancellationToken);
        }

        await using var factory = CreateFactory();
        using var client = CreateClient(factory, caller);

        using var response = await client.GetAsync($"{DirectoryPath}?query=ana", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SearchDirectory_NoToken_ReturnsUnauthorized()
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString, _azurite.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"{DirectoryPath}?query=ana", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SearchDirectory_OverRateLimit_ReturnsTooManyRequestsWithRetryAfter()
    {
        var caller = await TestCallers.SeedAsync(_fixture);
        _directory.SearchAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);
        await using var factory = new AndesMailerApiFactory(
            _fixture.ConnectionString,
            _azurite.ConnectionString,
            useTestAuthentication: true,
            configuration: new Dictionary<string, string?>
            {
                ["RateLimiting:DirectorySearch:TokenLimit"] = "1",
                ["RateLimiting:DirectorySearch:TokensPerPeriod"] = "1",
                ["RateLimiting:DirectorySearch:ReplenishmentPeriod"] = "01:00:00",
            },
            configureTestServices: services => services.AddScoped(_ => _directory));
        using var client = CreateClient(factory, caller);
        using var otherClient = CreateClient(factory, await TestCallers.SeedAsync(_fixture));

        using var first = await client.GetAsync($"{DirectoryPath}?query=ana", TestContext.Current.CancellationToken);
        using var second = await client.GetAsync($"{DirectoryPath}?query=anan", TestContext.Current.CancellationToken);
        using var otherCaller = await otherClient.GetAsync($"{DirectoryPath}?query=ana", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal(ProblemJson, second.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(second.Headers.RetryAfter);
        Assert.Equal(HttpStatusCode.OK, otherCaller.StatusCode);
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

    private static async Task<CurrentUserDto> GetMeAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<CurrentUserDto>(MePath, ApiJson.Options, TestContext.Current.CancellationToken))!;

    private static async Task<UserDto> GetUserAsync(HttpClient client, Guid id) =>
        (await client.GetFromJsonAsync<UserDto>($"{UsersPath}/{id}", TestContext.Current.CancellationToken))!;

    private AndesMailerApiFactory CreateFactory() => new(
        _fixture.ConnectionString,
        _azurite.ConnectionString,
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
