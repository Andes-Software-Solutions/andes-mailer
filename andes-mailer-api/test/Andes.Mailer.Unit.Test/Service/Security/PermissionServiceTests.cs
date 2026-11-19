using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Service.Security;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Unit.Test.Service.Security;

public sealed class PermissionServiceTests : IDisposable
{
    private readonly MailerDbFixture _fixture = new();

    public static TheoryData<Permissions> AllPermissions => [.. Enum.GetValues<Permissions>()];

    public static TheoryData<Roles, Permissions, TeamAccess> TeamRoleGrants => new()
    {
        { Roles.TeamAdministrator, Permissions.ViewTeam, TeamAccess.Granted },
        { Roles.TeamAdministrator, Permissions.ComposeTeamEmails, TeamAccess.Granted },
        { Roles.TeamAdministrator, Permissions.CanAttachDocuments, TeamAccess.Granted },
        { Roles.TeamAdministrator, Permissions.CreateTeamUser, TeamAccess.Granted },
        { Roles.TeamAdministrator, Permissions.ModifyTeamUser, TeamAccess.Granted },
        { Roles.TeamAdministrator, Permissions.DeactivateTeamUser, TeamAccess.Granted },
        { Roles.TeamAdministrator, Permissions.ManageTeamTemplates, TeamAccess.Granted },
        { Roles.TeamAdministrator, Permissions.DuplicateTeamEmails, TeamAccess.Granted },
        { Roles.TeamAdministrator, Permissions.SendTeamEmails, TeamAccess.Granted },
        { Roles.TeamAdministrator, Permissions.CreateTeam, TeamAccess.Forbidden },
        { Roles.TeamAdministrator, Permissions.CreateUser, TeamAccess.Forbidden },
        { Roles.TeamMember, Permissions.ViewTeam, TeamAccess.Granted },
        { Roles.TeamMember, Permissions.ComposeTeamEmails, TeamAccess.Granted },
        { Roles.TeamMember, Permissions.CanAttachDocuments, TeamAccess.Granted },
        { Roles.TeamMember, Permissions.CreateTeamUser, TeamAccess.Forbidden },
        { Roles.TeamMember, Permissions.ModifyTeamUser, TeamAccess.Forbidden },
        { Roles.TeamMember, Permissions.DeactivateTeamUser, TeamAccess.Forbidden },
        { Roles.TeamMember, Permissions.ManageTeamTemplates, TeamAccess.Forbidden },
        { Roles.TeamMember, Permissions.DuplicateTeamEmails, TeamAccess.Granted },
        { Roles.TeamMember, Permissions.SendTeamEmails, TeamAccess.Forbidden },
    };

    [Theory]
    [MemberData(nameof(AllPermissions))]
    public async Task HasPermissionAsync_Administrator_ReturnsTrue(Permissions permission)
    {
        var user = await _fixture.AddUserAsync("ada@contoso.com", role: Roles.Administrator);

        Assert.True(await HasPermissionAsync(user.Id, permission));
    }

    [Fact]
    public async Task HasPermissionAsync_TeamAdministratorOfATeam_GrantsNothingGlobally()
    {
        var team = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.AddTeamMemberAsync(team.Id, user.Id, Roles.TeamAdministrator);

        Assert.False(await HasPermissionAsync(user.Id, Permissions.CreateTeamUser));
    }

    [Fact]
    public async Task HasPermissionAsync_UserWithoutRole_ReturnsFalse()
    {
        var user = await _fixture.AddUserAsync("ada@contoso.com");

        Assert.False(await HasPermissionAsync(user.Id, Permissions.CreateUser));
    }

    [Fact]
    public async Task HasPermissionAsync_DeactivatedAdministrator_ReturnsFalse()
    {
        var user = await _fixture.AddUserAsync("ada@contoso.com", role: Roles.Administrator);
        await _fixture.DeactivateUserAsync(user.Id);

        Assert.False(await HasPermissionAsync(user.Id, Permissions.CreateUser));
    }

    [Fact]
    public async Task HasPermissionAsync_UnknownUser_ReturnsFalse() =>
        Assert.False(await HasPermissionAsync(Guid.NewGuid(), Permissions.CreateUser));

    [Fact]
    public async Task AddingGlobalTeamRole_ViolatesCheckConstraint()
    {
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        await using var ctx = _fixture.CreateContext();
        ctx.Add(new UserRole { UserId = user.Id, RoleId = Roles.TeamAdministrator });

        await Assert.ThrowsAsync<DbUpdateException>(() => ctx.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [MemberData(nameof(TeamRoleGrants))]
    public async Task CheckTeamPermissionAsync_Member_FollowsTheirTeamRole(Roles role, Permissions permission, TeamAccess expected)
    {
        var team = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.AddTeamMemberAsync(team.Id, user.Id, role);

        Assert.Equal(expected, await CheckTeamPermissionAsync(user.Id, team.Id, permission));
    }

    [Fact]
    public async Task CheckTeamPermissionAsync_AdministratorOutsideTheTeam_IsGranted()
    {
        var team = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com", role: Roles.Administrator);

        Assert.Equal(TeamAccess.Granted, await CheckTeamPermissionAsync(user.Id, team.Id, Permissions.ComposeTeamEmails));
    }

    [Fact]
    public async Task CheckTeamPermissionAsync_MemberOfAnotherTeam_IsNotFound()
    {
        var team = await _fixture.AddTeamAsync();
        var otherTeam = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.AddTeamMemberAsync(otherTeam.Id, user.Id, Roles.TeamAdministrator);

        Assert.Equal(TeamAccess.NotFound, await CheckTeamPermissionAsync(user.Id, team.Id, Permissions.ViewTeam));
    }

    [Fact]
    public async Task CheckTeamPermissionAsync_RemovedMember_IsNotFound()
    {
        var team = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        var member = await _fixture.AddTeamMemberAsync(team.Id, user.Id);
        await using (var ctx = _fixture.CreateContext())
        {
            ctx.Remove(await ctx.TeamMembers.SingleAsync(m => m.Id == member.Id, TestContext.Current.CancellationToken));
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(TeamAccess.NotFound, await CheckTeamPermissionAsync(user.Id, team.Id, Permissions.ViewTeam));
    }

    [Fact]
    public async Task CheckTeamPermissionAsync_DeactivatedUser_IsNotFound()
    {
        var team = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com");
        await _fixture.AddTeamMemberAsync(team.Id, user.Id, Roles.TeamAdministrator);
        await _fixture.DeactivateUserAsync(user.Id);

        Assert.Equal(TeamAccess.NotFound, await CheckTeamPermissionAsync(user.Id, team.Id, Permissions.ViewTeam));
    }

    [Fact]
    public async Task CheckTeamPermissionAsync_DeactivatedTeam_IsNotFoundEvenForAdministrator()
    {
        var team = await _fixture.AddTeamAsync();
        var user = await _fixture.AddUserAsync("ada@contoso.com", role: Roles.Administrator);
        await using (var ctx = _fixture.CreateContext())
        {
            ctx.Remove(await ctx.Teams.SingleAsync(t => t.Id == team.Id, TestContext.Current.CancellationToken));
            await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(TeamAccess.NotFound, await CheckTeamPermissionAsync(user.Id, team.Id, Permissions.ViewTeam));
    }

    [Fact]
    public async Task CheckTeamPermissionAsync_UnknownTeam_IsNotFound()
    {
        var user = await _fixture.AddUserAsync("ada@contoso.com", role: Roles.Administrator);

        Assert.Equal(TeamAccess.NotFound, await CheckTeamPermissionAsync(user.Id, Guid.NewGuid(), Permissions.ViewTeam));
    }

    public void Dispose() => _fixture.Dispose();

    #region Private methods

    private async Task<bool> HasPermissionAsync(Guid userId, Permissions permission)
    {
        await using var ctx = _fixture.CreateContext();

        return await new PermissionService(ctx).HasPermissionAsync(userId, permission, TestContext.Current.CancellationToken);
    }

    private async Task<TeamAccess> CheckTeamPermissionAsync(Guid userId, Guid teamId, Permissions permission)
    {
        await using var ctx = _fixture.CreateContext();

        return await new PermissionService(ctx).CheckTeamPermissionAsync(userId, teamId, permission, TestContext.Current.CancellationToken);
    }

    #endregion
}
