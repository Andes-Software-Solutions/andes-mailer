using Andes.Mailer.Common.Constants.ContactGroups;
using Andes.Mailer.Entity.ContactGroups;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.ContactGroups;

/// <summary>
/// Maps <see cref="ContactGroupMember"/> to <c>[Core].[ContactGroupMember]</c>.
/// </summary>
public sealed class ContactGroupMemberConfiguration : BaseModifiedEntityConfiguration<ContactGroupMember, Guid>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<ContactGroupMember> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("ContactGroupMember", SqlSchemas.Core);
        builder.Property(m => m.Address).HasMaxLength(ContactGroupLimits.AddressMaxLength).IsRequired();
        builder.Property(m => m.DisplayName).HasMaxLength(ContactGroupLimits.DisplayNameMaxLength);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.AddedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(m => m.RemovedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Active rows only: a removed address keeps its row for the audit trail, and adding it again creates a new one.
        builder.HasIndex(m => new { m.ContactGroupId, m.Address }).IsUnique().HasFilter("[DateDeactivated] IS NULL");

        // UserId is deliberately not a foreign key: a member can be any directory account, not only a user of this application.

        // Beside the row's own deactivation filter, so a deactivated group's members stay hidden with it.
        builder.HasQueryFilter("GroupDeactivation", m => m.ContactGroup!.DateDeactivated == null);
    }
}
