using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Emails;

/// <summary>
/// Maps <see cref="EmailRecipient"/> to <c>[Core].[EmailRecipient]</c>.
/// </summary>
public sealed class EmailRecipientConfiguration : IEntityTypeConfiguration<EmailRecipient>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<EmailRecipient> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EmailRecipient", SqlSchemas.Core);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Address).HasMaxLength(EmailRecipientLimits.AddressMaxLength).IsRequired();
        builder.Property(r => r.DisplayName).HasMaxLength(EmailRecipientLimits.DisplayNameMaxLength);
        builder.HasOne(r => r.RecipientType)
            .WithMany()
            .HasForeignKey(r => r.RecipientTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Across To, Cc, and Bcc: one address receives one copy.
        builder.HasIndex(r => new { r.EmailId, r.Address }).IsUnique();

        // UserId is deliberately not a foreign key: a recipient can be any directory account, not only a user of this application.

        // Mirrors the email's filter, so a deactivated email's recipients stay hidden with it.
        builder.HasQueryFilter("Deactivation", r => r.Email!.DateDeactivated == null);
    }
}
