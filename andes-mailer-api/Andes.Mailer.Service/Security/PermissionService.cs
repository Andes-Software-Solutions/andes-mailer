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
}

/// <summary>
/// Answers permission checks from the user, role, and permission tables.
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
}
