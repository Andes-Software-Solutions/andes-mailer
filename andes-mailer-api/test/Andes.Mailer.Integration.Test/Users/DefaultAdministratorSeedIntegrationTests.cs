using Andes.Mailer.Common.Enums;
using Andes.Mailer.Integration.Test.TestInfrastructure;
using Andes.Mailer.Service.Security;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Integration.Test.Users;

public sealed class DefaultAdministratorSeedIntegrationTests(SqlServerFixture fixture)
{
    private static readonly Guid _administratorId = new("4ee09cb0-d7e7-4be4-b82f-2674695eb9df");

    private readonly SqlServerFixture _fixture = fixture;

    public static TheoryData<Permissions> AllPermissions => [.. Enum.GetValues<Permissions>()];

    [Fact]
    public async Task Migrate_SeedsDefaultAdministrator()
    {
        await using var ctx = _fixture.CreateContext();

        var user = await ctx.Users.AsNoTracking().SingleAsync(u => u.Id == _administratorId, TestContext.Current.CancellationToken);

        Assert.Equal("rorro.irg@gmail.com", user.Email);
        Assert.NotEmpty(user.Version);
    }

    [Theory]
    [MemberData(nameof(AllPermissions))]
    public async Task Migrate_DefaultAdministrator_HoldsPermission(Permissions permission)
    {
        await using var ctx = _fixture.CreateContext();

        Assert.True(await new PermissionService(ctx).HasPermissionAsync(_administratorId, permission, TestContext.Current.CancellationToken));
    }
}
