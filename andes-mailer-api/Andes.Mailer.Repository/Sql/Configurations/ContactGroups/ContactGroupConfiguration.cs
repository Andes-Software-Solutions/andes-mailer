using Andes.Mailer.Common.Constants.ContactGroups;
using Andes.Mailer.Entity.ContactGroups;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.ContactGroups;

/// <summary>
/// Maps <see cref="ContactGroup"/> to <c>[Core].[ContactGroup]</c>.
/// </summary>
public sealed class ContactGroupConfiguration : BaseModifiedEntityConfiguration<ContactGroup, Guid>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<ContactGroup> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContactGroup", SqlSchemas.Core);
        builder.Property(g => g.Name).HasMaxLength(ContactGroupLimits.NameMaxLength).IsRequired();
        builder.Property(g => g.Description).HasMaxLength(ContactGroupLimits.DescriptionMaxLength);
        builder.HasOne(g => g.Team)
            .WithMany()
            .HasForeignKey(g => g.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(g => g.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(g => g.DeactivatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Active rows only, so a deactivated group's name can be used again.
        builder.HasIndex(g => new { g.TeamId, g.Name }).IsUnique().HasFilter("[DateDeactivated] IS NULL");

        // Beside the row's own deactivation filter, so a deactivated team's groups stay hidden with it.
        builder.HasQueryFilter("TeamDeactivation", g => g.Team!.DateDeactivated == null);

        builder.HasMany(g => g.Members)
            .WithOne(m => m.ContactGroup)
            .HasForeignKey(m => m.ContactGroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
