using Andes.Mailer.Common.Constants.Users;
using Andes.Mailer.Entity.Teams;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Configurations.Users;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Teams;

/// <summary>
/// Maps <see cref="TeamMember"/> to <c>[Core].[TeamMember]</c>.
/// </summary>
public sealed class TeamMemberConfiguration : BaseModifiedEntityConfiguration<TeamMember, Guid>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<TeamMember> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            "TeamMember",
            SqlSchemas.Core,
            t => t.HasCheckConstraint("CK_TeamMember_RoleId", RoleConstraint.Of(RoleScopes.TeamRoles)));
        builder.HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(m => m.Role)
            .WithMany()
            .HasForeignKey(m => m.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Over every row, not only active ones: re-adding a removed member reactivates their row rather than adding a second.
        builder.HasIndex(m => new { m.TeamId, m.UserId }).IsUnique();
        builder.HasIndex(m => m.UserId);

        // Beside the row's own deactivation filter, so a deactivated user or team grants nothing through the membership.
        builder.HasQueryFilter("UserDeactivation", m => m.User!.DateDeactivated == null);
        builder.HasQueryFilter("TeamDeactivation", m => m.Team!.DateDeactivated == null);
    }
}
