using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Users;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Repository.Sql.DbContexts;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Service.Users;

/// <summary>
/// Describes the signed-in user, with the roles, permissions, and teams that decide what they may do.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Gets the signed-in user's profile and access.
    /// </summary>
    /// <param name="userId">The user's Entra object id.</param>
    /// <param name="tokenName">The display name from the token, used when the account is not an active user.</param>
    /// <param name="tokenEmail">The sign-in address from the token, used when the account is not an active user.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>
    /// The user. An unknown or deactivated account is returned as inactive, with the token's name and address and no roles,
    /// permissions, or teams.
    /// </returns>
    Task<CurrentUserDto> GetAsync(Guid userId, string tokenName, string? tokenEmail, CancellationToken cancellationToken);
}

/// <summary>
/// Reads the signed-in user's access from the user, team, role, and permission tables.
/// </summary>
/// <param name="ctx">The store.</param>
public sealed class CurrentUserService(AndesMailerDbContext ctx) : ICurrentUserService
{
    private readonly AndesMailerDbContext _ctx = ctx;

    /// <inheritdoc/>
    public async Task<CurrentUserDto> GetAsync(Guid userId, string tokenName, string? tokenEmail, CancellationToken cancellationToken)
    {
        // The query filters drop a deactivated user, their roles and memberships, and deactivated teams, so every query
        // below sees only what still grants access.
        var user = await _ctx.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.FirstName, u.LastName, u.Email })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return new CurrentUserDto(userId, tokenName, tokenEmail, false, [], [], []);
        }

        var roles = await _ctx.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .Select(ur => ur.RoleId)
            .ToListAsync(cancellationToken);

        // The grant table holds a few rows per role, so one read beats a join per team.
        var grants = (await _ctx.RoleGrants
            .AsNoTracking()
            .Select(rg => new { rg.RoleId, rg.PermissionId })
            .ToListAsync(cancellationToken))
            .ToLookup(rg => rg.RoleId, rg => rg.PermissionId);
        var globalPermissions = roles.SelectMany(role => grants[role]).ToHashSet();

        var teams = _ctx.Teams.AsNoTracking();
        if (!globalPermissions.Contains(Permissions.ViewTeam))
        {
            teams = teams.Where(t => t.Members.Any(m => m.UserId == userId));
        }

        var visibleTeams = await teams
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Id)
            .Select(t => new
            {
                t.Id,
                t.Name,
                Role = t.Members.Where(m => m.UserId == userId).Select(m => (Roles?)m.RoleId).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        // A team role adds to the global grants, matching the fallback in PermissionService.CheckTeamPermissionAsync.
        List<CurrentUserTeamDto> teamAccess =
        [
            .. visibleTeams.Select(t => new CurrentUserTeamDto(
                t.Id,
                t.Name,
                t.Role,
                Sort(t.Role is { } role ? globalPermissions.Union(grants[role]) : globalPermissions))),
        ];

        var name = User.ComposeFullName(user.FirstName, user.LastName);

        return new CurrentUserDto(
            userId,
            name.Length > 0 ? name : tokenName,
            user.Email,
            true,
            [.. roles.Order()],
            Sort(globalPermissions),
            teamAccess);
    }

    #region Private methods

    private static List<Permissions> Sort(IEnumerable<Permissions> permissions) => [.. permissions.Distinct().Order()];

    #endregion
}
