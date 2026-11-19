using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Emails;

/// <summary>
/// Maps <see cref="EmailDelivery"/> to <c>[Core].[EmailDelivery]</c>.
/// </summary>
public sealed class EmailDeliveryConfiguration : BaseModifiedEntityConfiguration<EmailDelivery, Guid>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<EmailDelivery> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EmailDelivery", SqlSchemas.Core);
        builder.Property(d => d.GraphMessageId).HasMaxLength(EmailDeliveryLimits.GraphMessageIdMaxLength);
        builder.Property(d => d.LastError).HasMaxLength(EmailDeliveryLimits.LastErrorMaxLength);
        builder.HasOne(d => d.Status)
            .WithMany()
            .HasForeignKey(d => d.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        // Restrict, not cascade: the email already cascades to its recipients, and SQL Server refuses a second cascade path.
        builder.HasOne(d => d.Recipient)
            .WithMany()
            .HasForeignKey(d => d.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => new { d.EmailId, d.RecipientId }).IsUnique();

        // The dispatcher claims due rows by status and due time.
        builder.HasIndex(d => new { d.StatusId, d.NextAttemptAt });

        // Beside the row's own deactivation filter, so a deactivated email's deliveries stay hidden with it.
        builder.HasQueryFilter("EmailDeactivation", d => d.Email!.DateDeactivated == null);
    }
}
