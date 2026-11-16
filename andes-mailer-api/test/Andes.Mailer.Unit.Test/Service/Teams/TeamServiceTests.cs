using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Actions.Teams;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Security;
using Andes.Mailer.Service.Teams;
using Andes.Mailer.Service.Users;
using Andes.Mailer.Service.Users.Models;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

namespace Andes.Mailer.Unit.Test.Service.Teams;

public sealed class TeamServiceTests : IDisposable
{
    private readonly MailerDbFixture _fixture = new();
    private readonly IDirectoryUserService _directory = Substitute.For<IDirectoryUserService>();

    [Fact]
    public async Task CreateAsync_DirectoryMailbox_StoresTeamWithMailboxIdentity()
    {
        var mailbox = new DirectoryUser(Guid.NewGuid(), "Sales", string.Empty, "sales@contoso.com");
        _directory.FindByEmailAsync("sales@contoso.com", Arg.Any<CancellationToken>()).Returns(mailbox);
        await using var ctx = _fixture.CreateContext();

        var team = await CreateService(ctx).CreateAsync(new CreateTeamActionDto(" Sales ", "  ", " Sales@Contoso.com "), TestContext.Current.CancellationToken);

        Assert.Equal("Sales", team.Name);
        Assert.Null(team.Description);
        Assert.Equal("sales@contoso.com", team.ServiceAccountEmail);
        await using var readCtx = _fixture.CreateContext();
        var row = await readCtx.Teams.SingleAsync(t => t.Id == team.Id, TestContext.Current.CancellationToken);
        Assert.Equal(mailbox.Id, row.ServiceAccountId);
    }

    [Fact]
    public async Task CreateAsync_NoDirectoryMailbox_ThrowsNotFound()
    {
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateService(ctx).CreateAsync(new CreateTeamActionDto("Sales", null, "nobody@contoso.com"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_NameTaken_ThrowsConflict()
    {
        await _fixture.AddTeamAsync("Sales");
        _directory.FindByEmailAsync("new@contoso.com", Arg.Any<CancellationToken>()).Returns(new DirectoryUser(Guid.NewGuid(), "New", string.Empty, "new@contoso.com"));
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(ctx).CreateAsync(new CreateTeamActionDto("Sales", null, "new@contoso.com"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_ServiceAccountTaken_ThrowsConflict()
    {
        await _fixture.AddTeamAsync("Sales", "shared@contoso.com");
        _directory.FindByEmailAsync("shared@contoso.com", Arg.Any<CancellationToken>()).Returns(new DirectoryUser(Guid.NewGuid(), "Shared", string.Empty, "shared@contoso.com"));
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() =>
            CreateService(ctx).CreateAsync(new CreateTeamActionDto("Support", null, "shared@contoso.com"), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateAsync_NameOfDeactivatedTeam_IsAccepted()
    {
        var old = await _fixture.AddTeamAsync("Sales");
        await DeactivateAsync(old.Id);
        _directory.FindByEmailAsync("sales@contoso.com", Arg.Any<CancellationToken>()).Returns(new DirectoryUser(Guid.NewGuid(), "Sales", string.Empty, "sales@contoso.com"));
        await using var ctx = _fixture.CreateContext();

        var team = await CreateService(ctx).CreateAsync(new CreateTeamActionDto("Sales", null, "sales@contoso.com"), TestContext.Current.CancellationToken);

        Assert.NotEqual(old.Id, team.Id);
    }

    [Fact]
    public async Task UpdateAsync_SameServiceAccount_RenamesWithoutAskingDirectory()
    {
        var team = await _fixture.AddTeamAsync("Sales", "sales@contoso.com");
        await using var ctx = _fixture.CreateContext();

        var updated = await CreateService(ctx).UpdateAsync(
            team.Id,
            new UpdateTeamActionDto("Sales EMEA", "Europe", "SALES@contoso.com", Convert.ToBase64String(team.Version)),
            TestContext.Current.CancellationToken);

        Assert.Equal("Sales EMEA", updated.Name);
        Assert.Equal("Europe", updated.Description);
        await _directory.DidNotReceive().FindByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_NewServiceAccount_VerifiesItInDirectory()
    {
        var team = await _fixture.AddTeamAsync("Sales", "sales@contoso.com");
        var mailbox = new DirectoryUser(Guid.NewGuid(), "Orders", string.Empty, "orders@contoso.com");
        _directory.FindByEmailAsync("orders@contoso.com", Arg.Any<CancellationToken>()).Returns(mailbox);
        await using var ctx = _fixture.CreateContext();

        var updated = await CreateService(ctx).UpdateAsync(
            team.Id,
            new UpdateTeamActionDto("Sales", null, "orders@contoso.com", Convert.ToBase64String(team.Version)),
            TestContext.Current.CancellationToken);

        Assert.Equal("orders@contoso.com", updated.ServiceAccountEmail);
    }

    [Fact]
    public async Task UpdateAsync_StaleVersion_ThrowsConflict()
    {
        var team = await _fixture.AddTeamAsync("Sales", "sales@contoso.com");
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() => CreateService(ctx).UpdateAsync(
            team.Id,
            new UpdateTeamActionDto("Sales", null, "sales@contoso.com", Convert.ToBase64String(new byte[8])),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_NameOfAnotherTeam_ThrowsConflict()
    {
        await _fixture.AddTeamAsync("Support");
        var team = await _fixture.AddTeamAsync("Sales", "sales@contoso.com");
        await using var ctx = _fixture.CreateContext();

        await Assert.ThrowsAsync<ConflictException>(() => CreateService(ctx).UpdateAsync(
            team.Id,
            new UpdateTeamActionDto("Support", null, "sales@contoso.com", Convert.ToBase64String(team.Version)),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeactivateAsync_Team_HidesTeamAndItsMembers()
    {
        var team = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.AddTeamMemberAsync(team.Id, user.Id);

        await DeactivateAsync(team.Id);

        await using var ctx = _fixture.CreateContext();
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService(ctx).GetAsync(team.Id, TestContext.Current.CancellationToken));
        Assert.False(await ctx.TeamMembers.AnyAsync(m => m.TeamId == team.Id, TestContext.Current.CancellationToken));
        Assert.True(await ctx.Users.AnyAsync(u => u.Id == user.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ListAsync_Member_ReturnsOnlyOwnTeamsWithRole()
    {
        var sales = await _fixture.AddTeamAsync("Sales");
        await _fixture.AddTeamAsync("Support");
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.AddTeamMemberAsync(sales.Id, user.Id, Roles.TeamAdministrator);
        await using var ctx = _fixture.CreateContext();

        var page = await CreateService(ctx).ListAsync(user.Id, new ListTeamsActionDto(), TestContext.Current.CancellationToken);

        var item = Assert.Single(page.Items);
        Assert.Equal(sales.Id, item.Id);
        Assert.Equal(Roles.TeamAdministrator, item.Role);
        Assert.Equal(1, item.MemberCount);
    }

    [Fact]
    public async Task ListAsync_Administrator_ReturnsEveryTeamByName()
    {
        await _fixture.AddTeamAsync("Support");
        await _fixture.AddTeamAsync("Sales");
        var admin = await _fixture.AddUserAsync("ada@contoso.com", role: Roles.Administrator);
        await using var ctx = _fixture.CreateContext();

        var page = await CreateService(ctx).ListAsync(admin.Id, new ListTeamsActionDto(), TestContext.Current.CancellationToken);

        Assert.Equal(["Sales", "Support"], page.Items.Select(t => t.Name));
        Assert.All(page.Items, t => Assert.Null(t.Role));
    }

    public void Dispose() => _fixture.Dispose();

    #region Private methods

    private TeamService CreateService(AndesMailerDbContext ctx) => new(ctx, _directory, new PermissionService(ctx));

    private async Task DeactivateAsync(Guid id)
    {
        await using var ctx = _fixture.CreateContext();
        await CreateService(ctx).DeactivateAsync(id, TestContext.Current.CancellationToken);
    }

    #endregion
}
