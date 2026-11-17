using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Entity.Emails.Templates;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Emails.Templates;

/// <summary>
/// Maps <see cref="EmailTemplateRecipient"/> to <c>[Core].[EmailTemplateRecipient]</c>.
/// </summary>
public sealed class EmailTemplateRecipientConfiguration : IEntityTypeConfiguration<EmailTemplateRecipient>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<EmailTemplateRecipient> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EmailTemplateRecipient", SqlSchemas.Core);
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Address).HasMaxLength(EmailRecipientLimits.AddressMaxLength).IsRequired();
        builder.HasOne(r => r.RecipientType)
            .WithMany()
            .HasForeignKey(r => r.RecipientTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Across To, Cc, and Bcc, as on an email, so every template copies into an email the email's own index accepts.
        builder.HasIndex(r => new { r.TemplateId, r.Address }).IsUnique();

        // Mirrors the template's filter, so a deactivated template's recipients stay hidden with it.
        builder.HasQueryFilter("Deactivation", r => r.Template!.DateDeactivated == null);
    }
}
