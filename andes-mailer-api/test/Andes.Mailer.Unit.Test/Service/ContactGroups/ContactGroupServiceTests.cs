using Andes.Mailer.Dto.Actions.ContactGroups;
using Andes.Mailer.Dto.ContactGroups;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.ContactGroups;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

namespace Andes.Mailer.Unit.Test.Service.ContactGroups;

public sealed class ContactGroupServiceTests : IAsyncLifetime
{
    private readonly MailerDbFixture _fixture = new();
    private readonly IDirectoryUserService _directory = Substitute.For<IDirectoryUserService>();
    private Guid _callerId;

    public async ValueTask InitializeAsync() => _callerId = (await _fixture.AddUserAsync("caller@contoso.com")).Id;

    public ValueTask DisposeAsync()
    {
        _fixture.Dispose();

        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task CreateAsync_DirectoryAndExternalAddresses_NamesOnlyDirectoryMembers()
    {
        var grace = new DirectoryUser(Guid.NewGuid(), "Grace", "Hopper", "grace@contoso.com");
        _directory.FindByEmailsAsync(Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<string, DirectoryUser> { ["grace@contoso.com"] = grace });
        var team = await _fixture.AddTeamAsync();
        await using var ctx = _fixture.CreateContext();

        var group = await CreateService(ctx).CreateAsync(
            team.Id,
            _callerId,
            new CreateContactGroupActionDto(" Clients ", "  ", Members(" Grace@Contoso.com ", "buyer@fabrikam.com")),
            TestContext.Current.CancellationToken);

        Assert.Equal("Clients", group.Name);
        Assert.Null(group.Description);
        Assert.Equal(
            [new ContactGroupMemberDto("grace@contoso.com", grace.Id, "Grace Hopper"), new ContactGroupMemberDto("buyer@fabrikam.com", null, null)],
            group.Members);
    }

    [Fact]
    public async Task CreateAsync_NameTakenInTeam_ThrowsConflict()
    {
        var team = await _fixture.AddTeamAsync();
        await CreateGroupAsync(team.Id, "Clients", "a@fabrikam.com");
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() => CreateService(ctx).CreateAsync(
            team.Id,
            _callerId,
            new CreateContactGroupActionDto("Clients", null, Members("b@fabrikam.com")),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_NameTakenInAnotherTeam_CreatesGroup()
    {
        var other = await _fixture.AddTeamAsync();
        var team = await _fixture.AddTeamAsync();
        await CreateGroupAsync(other.Id, "Clients", "a@fabrikam.com");
        await using var ctx = _fixture.CreateContext();

        var group = await CreateService(ctx).CreateAsync(
            team.Id,
            _callerId,
            new CreateContactGroupActionDto("Clients", null, Members("b@fabrikam.com")),
            TestContext.Current.CancellationToken);

        Assert.Equal("Clients", group.Name);
    }

    [Fact]
    public async Task UpdateAsync_KeptAndNewMembers_LooksUpOnlyNewAddressesAndKeepsOrder()
    {
        var team = await _fixture.AddTeamAsync();
        var created = await CreateGroupAsync(team.Id, "Clients", "a@fabrikam.com", "b@fabrikam.com");
        _directory.ClearReceivedCalls();
        await using var ctx = _fixture.CreateContext();

        var updated = await CreateService(ctx).UpdateAsync(
            team.Id,
            created.Id,
            _callerId,
            new UpdateContactGroupActionDto("Key clients", "Top accounts", Members("c@fabrikam.com", "b@fabrikam.com"), created.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal("Key clients", updated.Name);
        Assert.Equal(["c@fabrikam.com", "b@fabrikam.com"], updated.Members.Select(m => m.Address));
        await _directory.Received(1).FindByEmailsAsync(
            Arg.Is<IReadOnlyCollection<string>>(a => a.Count == 1 && a.Contains("c@fabrikam.com")),
            Arg.Any<CancellationToken>());
        await using var readCtx = _fixture.CreateContext();
        Assert.Equal(2, await readCtx.ContactGroupMembers.CountAsync(m => m.ContactGroupId == created.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_MembersOnlyChange_StampsDateModified()
    {
        var team = await _fixture.AddTeamAsync();
        var created = await CreateGroupAsync(team.Id, "Clients", "a@fabrikam.com");
        await using var ctx = _fixture.CreateContext();

        var updated = await CreateService(ctx).UpdateAsync(
            team.Id,
            created.Id,
            _callerId,
            new UpdateContactGroupActionDto("Clients", null, Members("a@fabrikam.com", "b@fabrikam.com"), created.Version),
            TestContext.Current.CancellationToken);

        Assert.Null(created.DateModified);
        Assert.NotNull(updated.DateModified);
    }

    [Fact]
    public async Task UpdateAsync_DroppedMember_KeepsRowWithWhoRemovedItAndWhen()
    {
        var team = await _fixture.AddTeamAsync();
        var created = await CreateGroupAsync(team.Id, "Clients", "a@fabrikam.com", "b@fabrikam.com");
        var editor = (await _fixture.AddUserAsync("editor@contoso.com")).Id;
        await using var ctx = _fixture.CreateContext();

        await CreateService(ctx).UpdateAsync(
            team.Id,
            created.Id,
            editor,
            new UpdateContactGroupActionDto("Clients", null, Members("b@fabrikam.com", "c@fabrikam.com"), created.Version),
            TestContext.Current.CancellationToken);

        await using var readCtx = _fixture.CreateContext();
        var rows = await readCtx.ContactGroupMembers
            .IgnoreQueryFilters()
            .Where(m => m.ContactGroupId == created.Id)
            .ToDictionaryAsync(m => m.Address, TestContext.Current.CancellationToken);
        Assert.Equal(3, rows.Count);
        Assert.Equal(_fixture.Time.GetUtcNow(), rows["a@fabrikam.com"].DateDeactivated);
        Assert.Equal(editor, rows["a@fabrikam.com"].RemovedById);
        Assert.Equal(_callerId, rows["a@fabrikam.com"].AddedById);
        Assert.Null(rows["b@fabrikam.com"].RemovedById);
        Assert.Equal(editor, rows["c@fabrikam.com"].AddedById);
    }

    [Fact]
    public async Task UpdateAsync_AddressAddedAgainAfterRemoval_CreatesNewRow()
    {
        var team = await _fixture.AddTeamAsync();
        var created = await CreateGroupAsync(team.Id, "Clients", "a@fabrikam.com", "b@fabrikam.com");
        string version;
        await using (var ctx = _fixture.CreateContext())
        {
            version = (await CreateService(ctx).UpdateAsync(
                team.Id,
                created.Id,
                _callerId,
                new UpdateContactGroupActionDto("Clients", null, Members("b@fabrikam.com"), created.Version),
                TestContext.Current.CancellationToken)).Version;
        }

        await using (var ctx = _fixture.CreateContext())
        {
            var updated = await CreateService(ctx).UpdateAsync(
                team.Id,
                created.Id,
                _callerId,
                new UpdateContactGroupActionDto("Clients", null, Members("a@fabrikam.com", "b@fabrikam.com"), version),
                TestContext.Current.CancellationToken);

            Assert.Equal(["a@fabrikam.com", "b@fabrikam.com"], updated.Members.Select(m => m.Address));
        }

        await using var readCtx = _fixture.CreateContext();
        var rows = await readCtx.ContactGroupMembers
            .IgnoreQueryFilters()
            .Where(m => m.ContactGroupId == created.Id && m.Address == "a@fabrikam.com")
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, rows.Count);
        Assert.Single(rows, r => r.DateDeactivated is null);
    }

    [Fact]
    public async Task UpdateAsync_NameTakenByAnotherGroup_ThrowsConflict()
    {
        var team = await _fixture.AddTeamAsync();
        await CreateGroupAsync(team.Id, "Suppliers", "a@fabrikam.com");
        var created = await CreateGroupAsync(team.Id, "Clients", "a@fabrikam.com");
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() => CreateService(ctx).UpdateAsync(
            team.Id,
            created.Id,
            _callerId,
            new UpdateContactGroupActionDto(" Suppliers ", null, Members("a@fabrikam.com"), created.Version),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_SameName_KeepsGroup()
    {
        var team = await _fixture.AddTeamAsync();
        var created = await CreateGroupAsync(team.Id, "Clients", "a@fabrikam.com");
        await using var ctx = _fixture.CreateContext();

        var updated = await CreateService(ctx).UpdateAsync(
            team.Id,
            created.Id,
            _callerId,
            new UpdateContactGroupActionDto("Clients", "Renamed nothing", Members("a@fabrikam.com"), created.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal("Clients", updated.Name);
        Assert.Equal("Renamed nothing", updated.Description);
    }

    [Fact]
    public async Task UpdateAsync_StaleVersion_ThrowsConflict()
    {
        var team = await _fixture.AddTeamAsync();
        var created = await CreateGroupAsync(team.Id, "Clients", "a@fabrikam.com");
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() => CreateService(ctx).UpdateAsync(
            team.Id,
            created.Id,
            _callerId,
            new UpdateContactGroupActionDto("Clients", null, Members("a@fabrikam.com"), Convert.ToBase64String(new byte[8])),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetAsync_GroupOfAnotherTeam_ThrowsNotFound()
    {
        var team = await _fixture.AddTeamAsync();
        var other = await _fixture.AddTeamAsync();
        var created = await CreateGroupAsync(team.Id, "Clients", "a@fabrikam.com");
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(ctx).GetAsync(other.Id, created.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeactivateAsync_Group_HidesGroupRecordsDeactivatorAndFreesName()
    {
        var team = await _fixture.AddTeamAsync();
        var created = await CreateGroupAsync(team.Id, "Clients", "a@fabrikam.com");
        await using (var ctx = _fixture.CreateContext())
        {
            await CreateService(ctx).DeactivateAsync(team.Id, created.Id, _callerId, TestContext.Current.CancellationToken);
        }

        await using var readCtx = _fixture.CreateContext();
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(readCtx).GetAsync(team.Id, created.Id, TestContext.Current.CancellationToken));
        Assert.Equal(0, await readCtx.ContactGroupMembers.CountAsync(TestContext.Current.CancellationToken));
        var row = await readCtx.ContactGroups.IgnoreQueryFilters().SingleAsync(g => g.Id == created.Id, TestContext.Current.CancellationToken);
        Assert.Equal(_callerId, row.DeactivatedById);
        Assert.Equal(_fixture.Time.GetUtcNow(), row.DateDeactivated);
        var reused = await CreateGroupAsync(team.Id, "Clients", "b@fabrikam.com");
        Assert.NotEqual(created.Id, reused.Id);
    }

    [Fact]
    public async Task ListAsync_Search_ReturnsMatchingGroupsOfTeamByName()
    {
        var team = await _fixture.AddTeamAsync();
        var other = await _fixture.AddTeamAsync();
        await CreateGroupAsync(team.Id, "Suppliers", "a@fabrikam.com");
        await CreateGroupAsync(team.Id, "Key clients", "a@fabrikam.com", "b@fabrikam.com");
        await CreateGroupAsync(team.Id, "Clients", "a@fabrikam.com");
        await CreateGroupAsync(other.Id, "Other clients", "a@fabrikam.com");
        await using var ctx = _fixture.CreateContext();

        var page = await CreateService(ctx).ListAsync(team.Id, new ListContactGroupsActionDto(" lients "), TestContext.Current.CancellationToken);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal([("Clients", 1), ("Key clients", 2)], page.Items.Select(g => (g.Name, g.MemberCount)));
    }

    #region Private methods

    private static ContactGroupMemberActionDto[] Members(params string[] addresses) => [.. addresses.Select(a => new ContactGroupMemberActionDto(a))];

    private ContactGroupService CreateService(AndesMailerDbContext ctx) => new(ctx, _directory, _fixture.Time);

    private async Task<ContactGroupDto> CreateGroupAsync(Guid teamId, string name, params string[] addresses)
    {
        await using var ctx = _fixture.CreateContext();

        return await CreateService(ctx).CreateAsync(teamId, _callerId, new CreateContactGroupActionDto(name, null, Members(addresses)), TestContext.Current.CancellationToken);
    }

    #endregion
}
