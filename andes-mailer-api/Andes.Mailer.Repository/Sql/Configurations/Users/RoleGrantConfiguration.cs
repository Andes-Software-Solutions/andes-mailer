using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Users;

/// <summary>
/// Maps <see cref="RoleGrant"/> to <c>[Core.Ref].[RolePermission]</c> and seeds which permissions each role grants.
/// </summary>
public sealed class RoleGrantConfiguration : IEntityTypeConfiguration<RoleGrant>
{
    private static readonly Permissions[] _teamAdministratorPermissions =
    [
        Permissions.CreateTeamUser,
        Permissions.ModifyTeamUser,
        Permissions.DeactivateTeamUser,
    ];

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<RoleGrant> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("RolePermission", SqlSchemas.CoreRef);
        builder.HasKey(rg => new { rg.RoleId, rg.PermissionId });
        builder.HasOne(rg => rg.Role)
            .WithMany()
            .HasForeignKey(rg => rg.RoleId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(rg => rg.PermissionType)
            .WithMany()
            .HasForeignKey(rg => rg.PermissionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Application-owned like the enums themselves: a grant changes only with code, through a new migration.
        builder.HasData(
        [
            .. Enum.GetValues<Permissions>().Select(p => new RoleGrant { RoleId = Roles.Administrator, PermissionId = p }),
            .. _teamAdministratorPermissions.Select(p => new RoleGrant { RoleId = Roles.TeamAdministrator, PermissionId = p }),
        ]);
    }
}
