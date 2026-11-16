using Andes.Mailer.Common.Constants.Teams;
using Andes.Mailer.Entity.Teams;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Teams;

/// <summary>
/// Maps <see cref="Team"/> to <c>[Core].[Team]</c>.
/// </summary>
public sealed class TeamConfiguration : BaseModifiedEntityConfiguration<Team, Guid>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<Team> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Team", SqlSchemas.Core);
        builder.Property(t => t.Name).HasMaxLength(TeamLimits.NameMaxLength).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(TeamLimits.DescriptionMaxLength);
        builder.Property(t => t.ServiceAccountEmail).HasMaxLength(TeamLimits.ServiceAccountEmailMaxLength).IsRequired();

        // Active rows only, so a deactivated team never blocks its name or mailbox from being used again.
        builder.HasIndex(t => t.Name).IsUnique().HasFilter("[DateDeactivated] IS NULL");
        builder.HasIndex(t => t.ServiceAccountEmail).IsUnique().HasFilter("[DateDeactivated] IS NULL");
        // One mailbox can answer to several addresses, so the account id must be unique too, not just the address.
        builder.HasIndex(t => t.ServiceAccountId).IsUnique().HasFilter("[DateDeactivated] IS NULL");

        builder.HasMany(t => t.Members)
            .WithOne(m => m.Team)
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
