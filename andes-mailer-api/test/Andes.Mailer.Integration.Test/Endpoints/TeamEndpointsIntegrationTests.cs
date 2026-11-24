using System.Net;
using System.Net.Http.Json;

using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Emails;
using Andes.Mailer.Dto.Actions.Teams;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Dto.Teams;
using Andes.Mailer.Integration.Test.TestInfrastructure;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Andes.Mailer.Integration.Test.Endpoints;

public sealed class TeamEndpointsIntegrationTests(SqlServerFixture fixture, AzuriteFixture azurite)
{
    private const string TeamsPath = "/api/teams";

    private readonly SqlServerFixture _fixture = fixture;
    private readonly AzuriteFixture _azurite = azurite;
    private readonly IDirectoryUserService _directory = Substitute.For<IDirectoryUserService>();

    [Fact]
    public async Task CreateTeam_Administrator_ReturnsCreatedTeamWithDirectoryMailbox()
    {
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        var mailbox = StubMailbox();
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(TeamsPath, new CreateTeamActionDto(UniqueName(), "Invoices", mailbox.Email.ToUpperInvariant()), ApiJson.Options, TestContext.Current.CancellationToken);
        var team = await response.Content.ReadFromJsonAsync<TeamDto>(ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{TeamsPath}/{team!.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(mailbox.Email, team.ServiceAccountEmail);
    }

    [Fact]
    public async Task CreateTeam_TeamAdministrator_ReturnsForbidden()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var mailbox = StubMailbox();
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(TeamsPath, new CreateTeamActionDto(UniqueName(), null, mailbox.Email), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await _directory.DidNotReceive().FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTeam_MailboxNotInDirectory_ReturnsNotFound()
    {
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(TeamsPath, new CreateTeamActionDto(UniqueName(), null, "nobody@contoso.com"), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateTeam_NameTakenByActiveTeam_ReturnsConflict()
    {
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        var name = UniqueName();
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        using var first = await client.PostAsJsonAsync(TeamsPath, new CreateTeamActionDto(name, null, StubMailbox().Email), ApiJson.Options, TestContext.Current.CancellationToken);

        using var second = await client.PostAsJsonAsync(TeamsPath, new CreateTeamActionDto(name, null, StubMailbox().Email), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task GetTeam_Member_ReturnsTeam()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        var dto = await client.GetFromJsonAsync<TeamDto>($"{TeamsPath}/{team}", ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(team, dto!.Id);
    }

    [Fact]
    public async Task GetTeam_NotAMember_ReturnsNotFound()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedAsync(_fixture);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.GetAsync(new Uri($"{TeamsPath}/{team}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListTeams_Member_ReturnsOnlyOwnTeamsWithRole()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        var page = await client.GetFromJsonAsync<PaginatedResponseDto<TeamSummaryDto>>(TeamsPath, ApiJson.Options, TestContext.Current.CancellationToken);

        var item = Assert.Single(page!.Items);
        Assert.Equal(team, item.Id);
        Assert.Equal(Roles.TeamAdministrator, item.Role);
    }

    [Fact]
    public async Task UpdateTeam_StaleVersion_ReturnsConflict()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var current = await client.GetFromJsonAsync<TeamDto>($"{TeamsPath}/{team}", ApiJson.Options, TestContext.Current.CancellationToken);
        using var first = await client.PutAsJsonAsync($"{TeamsPath}/{team}", UpdateFrom(current!, UniqueName()), ApiJson.Options, TestContext.Current.CancellationToken);

        using var second = await client.PutAsJsonAsync($"{TeamsPath}/{team}", UpdateFrom(current!, UniqueName()), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task UpdateTeam_TeamAdministrator_SavesSettings()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var current = await client.GetFromJsonAsync<TeamDto>($"{TeamsPath}/{team}", ApiJson.Options, TestContext.Current.CancellationToken);
        var action = UpdateFrom(current!, UniqueName()) with
        {
            AvatarColor = TeamColors.Green,
            ConfirmRecipientsAbove = 200,
        };

        using var response = await client.PutAsJsonAsync($"{TeamsPath}/{team}", action, ApiJson.Options, TestContext.Current.CancellationToken);
        var updated = await response.Content.ReadFromJsonAsync<TeamDto>(ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TeamColors.Green, updated!.AvatarColor);
        Assert.Equal(200, updated.ConfirmRecipientsAbove);
        Assert.Equal(1, updated.AdministratorCount);
    }

    [Fact]
    public async Task UpdateTeam_TeamAdministratorChangesMailbox_ReturnsForbidden()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var mailbox = StubMailbox();
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var current = await client.GetFromJsonAsync<TeamDto>($"{TeamsPath}/{team}", ApiJson.Options, TestContext.Current.CancellationToken);

        using var response = await client.PutAsJsonAsync($"{TeamsPath}/{team}", UpdateFrom(current!, current!.Name) with { ServiceAccountEmail = mailbox.Email }, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await _directory.DidNotReceive().FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateTeam_TeamMember_ReturnsForbidden()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var current = await client.GetFromJsonAsync<TeamDto>($"{TeamsPath}/{team}", ApiJson.Options, TestContext.Current.CancellationToken);

        using var response = await client.PutAsJsonAsync($"{TeamsPath}/{team}", UpdateFrom(current!, UniqueName()), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTeam_OutsiderTeamAdministrator_ReturnsNotFound()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var otherTeam = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, otherTeam, Roles.TeamAdministrator);
        var admin = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        await using var factory = CreateFactory();
        using var adminClient = TestCallers.CreateClient(factory, admin);
        var current = await adminClient.GetFromJsonAsync<TeamDto>($"{TeamsPath}/{team}", ApiJson.Options, TestContext.Current.CancellationToken);
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PutAsJsonAsync($"{TeamsPath}/{team}", UpdateFrom(current!, UniqueName()), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateTeam_ThresholdOutOfRange_ReturnsBadRequest()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var current = await client.GetFromJsonAsync<TeamDto>($"{TeamsPath}/{team}", ApiJson.Options, TestContext.Current.CancellationToken);

        using var response = await client.PutAsJsonAsync($"{TeamsPath}/{team}", UpdateFrom(current!, UniqueName()) with { ConfirmRecipientsAbove = 0 }, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeactivateTeam_Administrator_HidesTeamAndItsEmails()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var member = await TestCallers.SeedMemberAsync(_fixture, team);
        var administrator = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        _directory.FindByEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>()).Returns(new Dictionary<string, DirectoryUser>());
        await using var factory = CreateFactory();
        using var memberClient = TestCallers.CreateClient(factory, member);
        using var created = await memberClient.PostAsJsonAsync($"{TeamsPath}/{team}/emails", new CreateEmailActionDto("Hi", string.Empty, []), ApiJson.Options, TestContext.Current.CancellationToken);
        var email = await created.Content.ReadFromJsonAsync<EmailDto>(ApiJson.Options, TestContext.Current.CancellationToken);
        using var adminClient = TestCallers.CreateClient(factory, administrator);

        using var deactivated = await adminClient.DeleteAsync(new Uri($"{TeamsPath}/{team}", UriKind.Relative), TestContext.Current.CancellationToken);
        using var getTeam = await adminClient.GetAsync(new Uri($"{TeamsPath}/{team}", UriKind.Relative), TestContext.Current.CancellationToken);
        using var getEmail = await memberClient.GetAsync(new Uri($"{TeamsPath}/{team}/emails/{email!.Id}", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, deactivated.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getTeam.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, getEmail.StatusCode);
    }

    #region Private methods

    private static string UniqueName() => $"Team {Guid.NewGuid():N}";

    private static UpdateTeamActionDto UpdateFrom(TeamDto team, string name) => new(
        name,
        team.Description,
        team.ServiceAccountEmail,
        team.AvatarColor,
        team.DefaultSignature,
        team.ConfirmRecipientsAbove,
        team.Version);

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
