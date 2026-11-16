using System.Net;
using System.Net.Http.Json;

using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Teams;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Dto.Teams;
using Andes.Mailer.Dto.Users;
using Andes.Mailer.Integration.Test.TestInfrastructure;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Andes.Mailer.Integration.Test.Endpoints;

public sealed class TeamMemberEndpointsIntegrationTests(SqlServerFixture fixture, AzuriteFixture azurite)
{
    private readonly SqlServerFixture _fixture = fixture;
    private readonly AzuriteFixture _azurite = azurite;
    private readonly IDirectoryUserService _directory = Substitute.For<IDirectoryUserService>();

    [Fact]
    public async Task AddMember_TeamAdministrator_AddsDirectoryPersonAsUserAndMember()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var person = new DirectoryUser(Guid.NewGuid(), "Grace", "Hopper", TestCallers.UniqueEmail());
        _directory.FindByEmailAsync(person.Email, Arg.Any<CancellationToken>()).Returns(person);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(MembersPath(team), new AddTeamMemberActionDto(person.Email, Roles.TeamMember), ApiJson.Options, TestContext.Current.CancellationToken);
        var member = await response.Content.ReadFromJsonAsync<TeamMemberDto>(ApiJson.Options, TestContext.Current.CancellationToken);
        var user = await client.GetFromJsonAsync<UserDto>($"/api/users/{person.Id}", ApiJson.Options, TestContext.Current.CancellationToken);
        var located = await client.GetFromJsonAsync<TeamMemberDto>(response.Headers.Location, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{MembersPath(team)}/{person.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(Roles.TeamMember, member!.Role);
        Assert.Equal("Grace Hopper", user!.FullName);
        Assert.Equal(member, located);
    }

    [Fact]
    public async Task AddMember_TeamMember_ReturnsForbidden()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(MembersPath(team), new AddTeamMemberActionDto(TestCallers.UniqueEmail(), Roles.TeamMember), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_AdministratorRole_ReturnsValidationProblem()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(MembersPath(team), new AddTeamMemberActionDto(TestCallers.UniqueEmail(), Roles.Administrator), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AddMember_TeamAdministratorOfAnotherTeam_ReturnsNotFound()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var otherTeam = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, otherTeam, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(MembersPath(team), new AddTeamMemberActionDto(TestCallers.UniqueEmail(), Roles.TeamMember), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMember_DemoteLastTeamAdministrator_ReturnsConflict()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var self = await GetMemberAsync(client, team, caller);

        using var response = await client.PutAsJsonAsync($"{MembersPath(team)}/{caller}", new UpdateTeamMemberActionDto(Roles.TeamMember, self.Version), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMember_PromoteThenDemoteSelf_LeavesOtherAdministrator()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var colleague = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var before = await GetMemberAsync(client, team, colleague);
        using var promote = await client.PutAsJsonAsync($"{MembersPath(team)}/{colleague}", new UpdateTeamMemberActionDto(Roles.TeamAdministrator, before.Version), ApiJson.Options, TestContext.Current.CancellationToken);
        var self = await GetMemberAsync(client, team, caller);

        using var demote = await client.PutAsJsonAsync($"{MembersPath(team)}/{caller}", new UpdateTeamMemberActionDto(Roles.TeamMember, self.Version), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);
        Assert.Equal(HttpStatusCode.OK, demote.StatusCode);
        Assert.Equal(Roles.TeamAdministrator, (await GetMemberAsync(client, team, colleague)).Role);
    }

    [Fact]
    public async Task RemoveMember_UserInTwoTeams_LosesAccessToThatTeamOnly()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var otherTeam = await TestCallers.SeedTeamAsync(_fixture);
        var administrator = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var member = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await TestCallers.AddMembershipAsync(_fixture, otherTeam, member);
        await using var factory = CreateFactory();
        using var adminClient = TestCallers.CreateClient(factory, administrator);
        using var memberClient = TestCallers.CreateClient(factory, member);

        using var removed = await adminClient.DeleteAsync(new Uri($"{MembersPath(team)}/{member}", UriKind.Relative), TestContext.Current.CancellationToken);
        using var removedTeam = await memberClient.GetAsync(new Uri($"/api/teams/{team}/emails", UriKind.Relative), TestContext.Current.CancellationToken);
        using var keptTeam = await memberClient.GetAsync(new Uri($"/api/teams/{otherTeam}/emails", UriKind.Relative), TestContext.Current.CancellationToken);
        using var user = await adminClient.GetAsync(new Uri($"/api/users/{member}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, removedTeam.StatusCode);
        Assert.Equal(HttpStatusCode.OK, keptTeam.StatusCode);
        Assert.Equal(HttpStatusCode.OK, user.StatusCode);
    }

    [Fact]
    public async Task ListMembers_Member_ReturnsRosterWithRoles()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var administrator = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        var page = await client.GetFromJsonAsync<PaginatedResponseDto<TeamMemberDto>>(MembersPath(team), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(2, page!.TotalCount);
        Assert.Equal(Roles.TeamAdministrator, page.Items.Single(m => m.UserId == administrator).Role);
        Assert.Equal(Roles.TeamMember, page.Items.Single(m => m.UserId == caller).Role);
    }

    #region Private methods

    private static string MembersPath(Guid teamId) => $"/api/teams/{teamId}/members";

    private static async Task<TeamMemberDto> GetMemberAsync(HttpClient client, Guid teamId, Guid userId)
    {
        var page = await client.GetFromJsonAsync<PaginatedResponseDto<TeamMemberDto>>(MembersPath(teamId), ApiJson.Options, TestContext.Current.CancellationToken);

        return page!.Items.Single(m => m.UserId == userId);
    }

    private AndesMailerApiFactory CreateFactory() => new(
        _fixture.ConnectionString,
        _azurite.ConnectionString,
        useTestAuthentication: true,
        configureTestServices: services => services.AddScoped(_ => _directory));

    #endregion
}
