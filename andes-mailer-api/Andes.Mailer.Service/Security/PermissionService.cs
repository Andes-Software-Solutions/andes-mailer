using Andes.Mailer.Common.Enums;
using Andes.Mailer.Repository.Sql.DbContexts;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Service.Security;

/// <summary>
/// Answers whether a user holds a permission through any of their roles.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Determines whether an active user holds a permission.
    /// </summary>
    /// <param name="userId">The user's Entra object id.</param>
    /// <param name="permission">One of the enumeration values that specifies the permission to check.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns><see langword="true"/> if one of the user's roles grants the permission; otherwise, <see langword="false"/>. A deactivated or unknown user holds none.</returns>
    Task<bool> HasPermissionAsync(Guid userId, Permissions permission, CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether an active user holds a permission in a team, through a global role or through their role in the team.
    /// </summary>
    /// <param name="userId">The user's Entra object id.</param>
    /// <param name="teamId">The team's id.</param>
    /// <param name="permission">One of the enumeration values that specifies the permission to check.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>
    /// <see cref="TeamAccess.Granted"/> if the user holds the permission; <see cref="TeamAccess.Forbidden"/> if they are a member whose role does not grant it;
    /// otherwise, <see cref="TeamAccess.NotFound"/>, so a caller outside the team cannot tell whether it exists.
    /// </returns>
    Task<TeamAccess> CheckTeamPermissionAsync(Guid userId, Guid teamId, Permissions permission, CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether a signed-in account is an active user of this application.
    /// </summary>
    /// <param name="userId">The account's Entra object id.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns><see langword="true"/> if the account has an active user row; otherwise, <see langword="false"/>.</returns>
    Task<bool> IsActiveUserAsync(Guid userId, CancellationToken cancellationToken);
}

/// <summary>
/// Answers permission checks from the user, team, role, and permission tables.
/// </summary>
/// <param name="ctx">The store.</param>
public sealed class PermissionService(AndesMailerDbContext ctx) : IPermissionService
{
    private readonly AndesMailerDbContext _ctx = ctx;

    /// <inheritdoc/>
    public Task<bool> HasPermissionAsync(Guid userId, Permissions permission, CancellationToken cancellationToken)
    {
        // The UserRole query filter drops a deactivated user's roles, so no separate user check is needed.
        return _ctx.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .AnyAsync(
                ur => _ctx.RoleGrants.Any(rg => rg.RoleId == ur.RoleId && rg.PermissionId == permission),
                cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<TeamAccess> CheckTeamPermissionAsync(Guid userId, Guid teamId, Permissions permission, CancellationToken cancellationToken)
    {
        // Membership first, as most callers are members: the membership filters drop a deactivated user's or team's rows, so a
        // grant here needs no other check. Null when there is no active membership, which keeps a non-member apart from a member
        // whose role falls short.
        var granted = await _ctx.TeamMembers
            .AsNoTracking()
            .Where(m => m.TeamId == teamId && m.UserId == userId)
            .Select(m => (bool?)_ctx.RoleGrants.Any(rg => rg.RoleId == m.RoleId && rg.PermissionId == permission))
            .FirstOrDefaultAsync(cancellationToken);
        if (granted is true)
        {
            return TeamAccess.Granted;
        }

        // A deactivated team is out of reach even for a global role, so its emails cannot change while nobody can see the team.
        if (!await _ctx.Teams.AsNoTracking().AnyAsync(t => t.Id == teamId, cancellationToken))
        {
            return TeamAccess.NotFound;
        }

        if (await HasPermissionAsync(userId, permission, cancellationToken))
        {
            return TeamAccess.Granted;
        }

        return granted is null ? TeamAccess.NotFound : TeamAccess.Forbidden;
    }

    /// <inheritdoc/>
    public Task<bool> IsActiveUserAsync(Guid userId, CancellationToken cancellationToken) =>
        _ctx.Users.AsNoTracking().AnyAsync(u => u.Id == userId, cancellationToken);
}
