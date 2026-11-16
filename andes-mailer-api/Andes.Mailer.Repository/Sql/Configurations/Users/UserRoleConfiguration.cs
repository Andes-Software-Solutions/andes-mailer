using Andes.Mailer.Common.Constants.Users;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Users;

/// <summary>
/// Maps <see cref="UserRole"/> to <c>[Core].[UserRole]</c>.
/// </summary>
public sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Team roles are held through [Core].[TeamMember], where they name the team they apply to.
        builder.ToTable(
            "UserRole",
            SqlSchemas.Core,
            t => t.HasCheckConstraint("CK_UserRole_RoleId", RoleConstraint.Of(RoleScopes.GlobalRoles)));
        builder.HasKey(ur => new { ur.UserId, ur.RoleId });
        builder.HasOne(ur => ur.Role)
            .WithMany()
            .HasForeignKey(ur => ur.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Mirrors the user's filter, so a deactivated user's roles grant nothing and the required navigation stays consistent.
        builder.HasQueryFilter("Deactivation", ur => ur.User!.DateDeactivated == null);
    }
}
