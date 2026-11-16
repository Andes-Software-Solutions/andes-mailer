using Andes.Mailer.Common.Constants;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Teams;
using Andes.Mailer.Entity.Users;

namespace Andes.Mailer.Integration.Test.TestInfrastructure;

// Seeds signed-in callers and the clients that act as them.
public static class TestCallers
{
    #region Public static methods

    // Unique per call: the suite shares one database, and an active email must be unique.
    public static string UniqueEmail() => $"user-{Guid.NewGuid():N}@contoso.com";

    public static async Task<Guid> SeedAsync(SqlServerFixture fixture, Roles? role = null)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await using var ctx = fixture.CreateContext();
        var user = new User { Id = Guid.NewGuid(), FirstName = "Ada", LastName = "Lovelace", Email = UniqueEmail() };
        ctx.Add(user);
        if (role is { } granted)
        {
            ctx.Add(new UserRole { UserId = user.Id, RoleId = granted });
        }

        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return user.Id;
    }

    public static async Task<Guid> SeedTeamAsync(SqlServerFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await using var ctx = fixture.CreateContext();
        var id = Guid.NewGuid();
        var team = new Team { Id = id, Name = $"Team {id:N}", ServiceAccountEmail = $"mailbox-{id:N}@contoso.com", ServiceAccountId = Guid.NewGuid() };
        ctx.Add(team);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return team.Id;
    }

    // Seeds a new user who belongs to the team with the role.
    public static async Task<Guid> SeedMemberAsync(SqlServerFixture fixture, Guid teamId, Roles role = Roles.TeamMember)
    {
        var userId = await SeedAsync(fixture);
        await AddMembershipAsync(fixture, teamId, userId, role);

        return userId;
    }

    public static async Task AddMembershipAsync(SqlServerFixture fixture, Guid teamId, Guid userId, Roles role = Roles.TeamMember)
    {
        ArgumentNullException.ThrowIfNull(fixture);

        await using var ctx = fixture.CreateContext();
        ctx.Add(new TeamMember { TeamId = teamId, UserId = userId, RoleId = role });
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public static HttpClient CreateClient(AndesMailerApiFactory factory, Guid callerId)
    {
        ArgumentNullException.ThrowIfNull(factory);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.ScopesHeader, ApiScopes.AccessAsUser);
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, callerId.ToString());

        return client;
    }

    #endregion
}
