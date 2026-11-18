using System.Net;
using System.Net.Http.Json;

using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.ContactGroups;
using Andes.Mailer.Dto.ContactGroups;
using Andes.Mailer.Dto.Pagination;
using Andes.Mailer.Integration.Test.TestInfrastructure;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;

using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

namespace Andes.Mailer.Integration.Test.Endpoints;

public sealed class ContactGroupEndpointsIntegrationTests(SqlServerFixture fixture, AzuriteFixture azurite)
{
    private readonly SqlServerFixture _fixture = fixture;
    private readonly AzuriteFixture _azurite = azurite;
    private readonly IDirectoryUserService _directory = Substitute.For<IDirectoryUserService>();

    [Fact]
    public async Task CreateContactGroup_Administrator_ReturnsCreatedGroupWithDirectoryNames()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedAsync(_fixture, Roles.Administrator);
        var person = new DirectoryUser(Guid.NewGuid(), "Grace", "Hopper", TestCallers.UniqueEmail());
        _directory.FindByEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, DirectoryUser> { [person.Email] = person });
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(
            GroupsPath(team),
            new CreateContactGroupActionDto("Clients", "Monthly invoices", Members(person.Email, "buyer@fabrikam.com")),
            ApiJson.Options,
            TestContext.Current.CancellationToken);
        var group = await response.Content.ReadFromJsonAsync<ContactGroupDto>(ApiJson.Options, TestContext.Current.CancellationToken);
        var located = await client.GetFromJsonAsync<ContactGroupDto>(response.Headers.Location, ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal($"{GroupsPath(team)}/{group!.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(
            [new ContactGroupMemberDto(person.Email, person.Id, "Grace Hopper"), new ContactGroupMemberDto("buyer@fabrikam.com", null, null)],
            group.Members);
        Assert.Equal(group.Members, located!.Members);
    }

    [Fact]
    public async Task CreateContactGroup_TeamAdministrator_ReturnsCreated()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(GroupsPath(team), new CreateContactGroupActionDto("Clients", null, Members("buyer@fabrikam.com")), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateContactGroup_TeamMember_ReturnsForbidden()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(GroupsPath(team), new CreateContactGroupActionDto("Clients", null, Members("buyer@fabrikam.com")), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateContactGroup_DuplicateName_ReturnsConflict()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        await CreateGroupAsync(client, team, "Clients");

        using var response = await client.PostAsJsonAsync(GroupsPath(team), new CreateContactGroupActionDto("Clients", null, Members("other@fabrikam.com")), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateContactGroup_NoMembers_ReturnsValidationProblem()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.PostAsJsonAsync(GroupsPath(team), new CreateContactGroupActionDto("Clients", null, []), ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ListContactGroups_TeamMember_ReturnsMatchingGroups()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var administrator = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        using var adminClient = TestCallers.CreateClient(factory, administrator);
        await CreateGroupAsync(adminClient, team, "Key clients");
        await CreateGroupAsync(adminClient, team, "Suppliers");
        using var client = TestCallers.CreateClient(factory, caller);

        var page = await client.GetFromJsonAsync<PaginatedResponseDto<ContactGroupSummaryDto>>($"{GroupsPath(team)}?search=CLIENT", ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal("Key clients", Assert.Single(page!.Items).Name);
    }

    [Fact]
    public async Task GetContactGroup_NotAMember_ReturnsNotFound()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var administrator = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var outsider = await TestCallers.SeedMemberAsync(_fixture, await TestCallers.SeedTeamAsync(_fixture), Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var adminClient = TestCallers.CreateClient(factory, administrator);
        var group = await CreateGroupAsync(adminClient, team, "Clients");
        using var client = TestCallers.CreateClient(factory, outsider);

        using var response = await client.GetAsync($"{GroupsPath(team)}/{group.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateContactGroup_MembersOnly_ReplacesMembersAndChangesVersion()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var group = await CreateGroupAsync(client, team, "Clients");

        using var response = await client.PutAsJsonAsync(
            $"{GroupsPath(team)}/{group.Id}",
            new UpdateContactGroupActionDto(group.Name, group.Description, Members("new@fabrikam.com", "buyer@fabrikam.com"), group.Version),
            ApiJson.Options,
            TestContext.Current.CancellationToken);
        var updated = await response.Content.ReadFromJsonAsync<ContactGroupDto>(ApiJson.Options, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["new@fabrikam.com", "buyer@fabrikam.com"], updated!.Members.Select(m => m.Address));
        Assert.NotEqual(group.Version, updated.Version);
    }

    [Fact]
    public async Task UpdateContactGroup_NameOfAnotherGroup_ReturnsConflict()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        await CreateGroupAsync(client, team, "Suppliers");
        var group = await CreateGroupAsync(client, team, "Clients");

        using var response = await client.PutAsJsonAsync(
            $"{GroupsPath(team)}/{group.Id}",
            new UpdateContactGroupActionDto("suppliers", null, Members("buyer@fabrikam.com"), group.Version),
            ApiJson.Options,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task UpdateContactGroup_StaleVersion_ReturnsConflict()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var group = await CreateGroupAsync(client, team, "Clients");
        using var first = await client.PutAsJsonAsync(
            $"{GroupsPath(team)}/{group.Id}",
            new UpdateContactGroupActionDto("Key clients", null, Members("buyer@fabrikam.com"), group.Version),
            ApiJson.Options,
            TestContext.Current.CancellationToken);

        using var response = await client.PutAsJsonAsync(
            $"{GroupsPath(team)}/{group.Id}",
            new UpdateContactGroupActionDto("Clients", null, Members("buyer@fabrikam.com"), group.Version),
            ApiJson.Options,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task DeactivateContactGroup_TeamAdministrator_HidesGroup()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        await using var factory = CreateFactory();
        using var client = TestCallers.CreateClient(factory, caller);
        var group = await CreateGroupAsync(client, team, "Clients");

        using var response = await client.DeleteAsync($"{GroupsPath(team)}/{group.Id}", TestContext.Current.CancellationToken);
        using var read = await client.GetAsync($"{GroupsPath(team)}/{group.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
    }

    [Fact]
    public async Task DeactivateContactGroup_TeamMember_ReturnsForbidden()
    {
        var team = await TestCallers.SeedTeamAsync(_fixture);
        var administrator = await TestCallers.SeedMemberAsync(_fixture, team, Roles.TeamAdministrator);
        var caller = await TestCallers.SeedMemberAsync(_fixture, team);
        await using var factory = CreateFactory();
        using var adminClient = TestCallers.CreateClient(factory, administrator);
        var group = await CreateGroupAsync(adminClient, team, "Clients");
        using var client = TestCallers.CreateClient(factory, caller);

        using var response = await client.DeleteAsync($"{GroupsPath(team)}/{group.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #region Private methods

    private static string GroupsPath(Guid teamId) => $"/api/teams/{teamId}/contact-groups";

    private static ContactGroupMemberActionDto[] Members(params string[] addresses) => [.. addresses.Select(a => new ContactGroupMemberActionDto(a))];

    private static async Task<ContactGroupDto> CreateGroupAsync(HttpClient client, Guid teamId, string name)
    {
        using var response = await client.PostAsJsonAsync(GroupsPath(teamId), new CreateContactGroupActionDto(name, null, Members("buyer@fabrikam.com")), ApiJson.Options, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ContactGroupDto>(ApiJson.Options, TestContext.Current.CancellationToken))!;
    }

    private AndesMailerApiFactory CreateFactory() => new(
        _fixture.ConnectionString,
        _azurite.ConnectionString,
        useTestAuthentication: true,
        configureTestServices: services => services.AddScoped(_ => _directory));

    #endregion
}
