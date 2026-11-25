using System.Net;
using System.Net.Http.Json;

using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Teams;
using Andes.Mailer.Dto.Teams;
using Andes.Mailer.Integration.Test.TestInfrastructure;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Andes.Mailer.Integration.Test.Endpoints;

public sealed class IdempotencyIntegrationTests(SqlServerFixture fixture, AzuriteFixture azurite)
{
    private const string TeamsPath = "/api/teams";
    private const string KeyHeader = "Idempotency-Key";
    private const string ReplayedHeader = "Idempotent-Replayed";

    private readonly SqlServerFixture _fixture = fixture;
    private readonly AzuriteFixture _azurite = azurite;
    private readonly IDirectoryUserService _directory = Substitute.For<IDirectoryUserService>();

    [Fact]
    public async Task CreateTeam_SameKeyTwice_ReplaysFirstResponseAndCreatesOneTeam()
    {
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        var action = new CreateTeamActionDto(UniqueName(), null, StubMailbox().Email);
        var key = Guid.NewGuid().ToString();
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var first = await SendAsync(client, HttpMethod.Post, TeamsPath, action, key);
        using var second = await SendAsync(client, HttpMethod.Post, TeamsPath, action, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.False(first.Headers.Contains(ReplayedHeader));
        Assert.Equal(["true"], second.Headers.GetValues(ReplayedHeader));
        Assert.Equal(first.Headers.Location, second.Headers.Location);
        var firstTeam = await first.Content.ReadFromJsonAsync<TeamDto>(ApiJson.Options, TestContext.Current.CancellationToken);
        var secondTeam = await second.Content.ReadFromJsonAsync<TeamDto>(ApiJson.Options, TestContext.Current.CancellationToken);
        Assert.Equal(firstTeam, secondTeam);
        await using var ctx = _fixture.CreateContext();
        Assert.Equal(1, await ctx.Teams.CountAsync(t => t.Name == action.Name, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateTeam_SameKeyDifferentBody_ReturnsUnprocessable()
    {
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        var mailbox = StubMailbox().Email;
        var key = Guid.NewGuid().ToString();
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var first = await SendAsync(client, HttpMethod.Post, TeamsPath, new CreateTeamActionDto(UniqueName(), null, mailbox), key);
        using var second = await SendAsync(client, HttpMethod.Post, TeamsPath, new CreateTeamActionDto(UniqueName(), null, mailbox), key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
    }

    [Fact]
    public async Task CreateTeam_SameKeyFromTwoCallers_RunsBoth()
    {
        var alice = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        var bob = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        var key = Guid.NewGuid().ToString();
        await using var factory = CreateFactory();
        using var aliceClient = TestCallers.CreateClient(factory, alice);
        using var bobClient = TestCallers.CreateClient(factory, bob);

        using var first = await SendAsync(aliceClient, HttpMethod.Post, TeamsPath, new CreateTeamActionDto(UniqueName(), null, StubMailbox().Email), key);
        using var second = await SendAsync(bobClient, HttpMethod.Post, TeamsPath, new CreateTeamActionDto(UniqueName(), null, StubMailbox().Email), key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        Assert.False(second.Headers.Contains(ReplayedHeader));
    }

    [Fact]
    public async Task CreateTeam_KeyOfFailedRequest_CanBeReusedForFixedRequest()
    {
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        var key = Guid.NewGuid().ToString();
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var failed = await SendAsync(client, HttpMethod.Post, TeamsPath, new CreateTeamActionDto(UniqueName(), null, "nobody@contoso.com"), key);
        using var fixedRequest = await SendAsync(client, HttpMethod.Post, TeamsPath, new CreateTeamActionDto(UniqueName(), null, StubMailbox().Email), key);

        Assert.Equal(HttpStatusCode.NotFound, failed.StatusCode);
        Assert.Equal(HttpStatusCode.Created, fixedRequest.StatusCode);
    }

    [Fact]
    public async Task DeactivateTeam_SameKeyTwice_ReplaysNoContentInsteadOfNotFound()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        var key = Guid.NewGuid().ToString();
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var first = await SendAsync(client, HttpMethod.Delete, $"{TeamsPath}/{team}", null, key);
        using var second = await SendAsync(client, HttpMethod.Delete, $"{TeamsPath}/{team}", null, key);
        using var withoutKey = await SendAsync(client, HttpMethod.Delete, $"{TeamsPath}/{team}", null, null);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, withoutKey.StatusCode);
    }

    [Fact]
    public async Task AddTeamMember_ParallelRequestsWithSameKey_AddOneMembership()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        var person = new DirectoryUser(Guid.NewGuid(), "Grace", "Hopper", TestCallers.UniqueEmail());
        _directory.FindByEmailAsync(person.Email, Arg.Any<CancellationToken>()).Returns(person);
        var action = new AddTeamMemberActionDto(person.Email, Roles.TeamMember);
        var key = Guid.NewGuid().ToString();
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SendAsync(client, HttpMethod.Post, $"{TeamsPath}/{team}/members", action, key)));

        // Each one either ran, replayed the first, or met it still running; none adds a second row or reports a duplicate.
        Assert.All(responses, r => Assert.True(r.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict, $"Unexpected {r.StatusCode}"));
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Created && !r.Headers.Contains(ReplayedHeader));
        foreach (var conflict in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
        {
            Assert.NotNull(conflict.Headers.RetryAfter);
        }

        await using var ctx = _fixture.CreateContext();
        Assert.Equal(1, await ctx.TeamMembers.CountAsync(m => m.TeamId == team && m.UserId == person.Id, TestContext.Current.CancellationToken));
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Fact]
    public async Task LeaveTeam_SameKeyTwice_ReplaysNoContent()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team);
        var key = Guid.NewGuid().ToString();
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var first = await SendAsync(client, HttpMethod.Delete, $"{TeamsPath}/{team}/members/me", null, key);
        using var second = await SendAsync(client, HttpMethod.Delete, $"{TeamsPath}/{team}/members/me", null, key);

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(["true"], second.Headers.GetValues(ReplayedHeader));
    }

    [Fact]
    public async Task RemoveTeamMember_TeamAdministratorRemovesSelfTwice_ReplaysNoContent()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var key = Guid.NewGuid().ToString();
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var first = await SendAsync(client, HttpMethod.Delete, $"{TeamsPath}/{team}/members/{caller}", null, key);
        using var second = await SendAsync(client, HttpMethod.Delete, $"{TeamsPath}/{team}/members/{caller}", null, key);
        using var fresh = await SendAsync(client, HttpMethod.Delete, $"{TeamsPath}/{team}/members/{caller}", null, Guid.NewGuid().ToString());

        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, fresh.StatusCode);
    }

    [Fact]
    public async Task CreateTeam_ForbiddenWithKey_ChecksPermissionAgainOnRetry()
    {
        var teamAdmin = await TestCallers.SeedMemberAsync(_fixture, await TestCallers.SeedTeamAsync(_fixture), Roles.TeamAdministrator);
        var key = Guid.NewGuid().ToString();
        var action = new CreateTeamActionDto(UniqueName(), null, StubMailbox().Email);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, teamAdmin);

        using var first = await SendAsync(client, HttpMethod.Post, TeamsPath, action, key);
        using var second = await SendAsync(client, HttpMethod.Post, TeamsPath, action, key);

        Assert.Equal(HttpStatusCode.Forbidden, first.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
    }

    [Fact]
    public async Task CreateTeam_MalformedKey_ReturnsBadRequest()
    {
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await SendAsync(client, HttpMethod.Post, TeamsPath, new CreateTeamActionDto(UniqueName(), null, StubMailbox().Email), "has space");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #region Private methods

    private static string UniqueName() => $"Team {Guid.NewGuid():N}";

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string path, object? body, string? key)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType(), options: ApiJson.Options);
        }

        if (key is not null)
        {
            request.Headers.TryAddWithoutValidation(KeyHeader, key);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    private DirectoryUser StubMailbox()
    {
        var mailbox = new DirectoryUser(Guid.NewGuid(), "Mailbox", string.Empty, $"mailbox-{Guid.NewGuid():N}@contoso.com");
        _directory.FindByEmailAsync(mailbox.Email, Arg.Any<CancellationToken>()).Returns(mailbox);

        return mailbox;
    }

    private AndesMailerApiFactory CreateFactory() => new(
        _fixture.ConnectionString,
        _azurite.ConnectionString,
        useTestAuthentication: true,
        configureTestServices: services => services.AddScoped(_ => _directory));

    #endregion
}
