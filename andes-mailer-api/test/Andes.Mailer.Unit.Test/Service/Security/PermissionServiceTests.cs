using Andes.Mailer.Common.Enums;
using Andes.Mailer.Service.Security;
using Andes.Mailer.Unit.Test.TestInfrastructure;

namespace Andes.Mailer.Unit.Test.Service.Security;

public sealed class PermissionServiceTests : IDisposable
{
    private readonly MailerDbFixture _fixture = new();

    public static TheoryData<Permissions> AllPermissions => [.. Enum.GetValues<Permissions>()];

    public static TheoryData<Permissions, bool> TeamAdministratorGrants => new()
    {
        { Permissions.CreateUser, false },
        { Permissions.ModifyUser, false },
        { Permissions.DeactivateUser, false },
        { Permissions.CreateTeamUser, true },
        { Permissions.ModifyTeamUser, true },
        { Permissions.DeactivateTeamUser, true },
    };

    [Theory]
    [MemberData(nameof(AllPermissions))]
    public async Task HasPermissionAsync_Administrator_ReturnsTrue(Permissions permission)
    {
        var user = await _fixture.AddUserAsync("ada@contoso.com", role: Roles.Administrator);

        Assert.True(await HasPermissionAsync(user.Id, permission));
    }

    [Theory]
    [MemberData(nameof(TeamAdministratorGrants))]
    public async Task HasPermissionAsync_TeamAdministrator_ReturnsTeamPermissionsOnly(Permissions permission, bool expected)
    {
        var user = await _fixture.AddUserAsync("ada@contoso.com", role: Roles.TeamAdministrator);

        Assert.Equal(expected, await HasPermissionAsync(user.Id, permission));
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

    public void Dispose() => _fixture.Dispose();

    #region Private methods

    private async Task<bool> HasPermissionAsync(Guid userId, Permissions permission)
    {
        await using var ctx = _fixture.CreateContext();

        return await new PermissionService(ctx).HasPermissionAsync(userId, permission, TestContext.Current.CancellationToken);
    }

    #endregion
}
