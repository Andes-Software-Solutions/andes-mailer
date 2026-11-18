using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Emails;

/// <summary>
/// Maps <see cref="EmailAttachment"/> to <c>[Core].[EmailAttachment]</c>.
/// </summary>
public sealed class EmailAttachmentConfiguration : BaseModifiedEntityConfiguration<EmailAttachment, Guid>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<EmailAttachment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EmailAttachment", SqlSchemas.Core);
        builder.Property(a => a.FileName).HasMaxLength(EmailAttachmentLimits.FileNameMaxLength).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(EmailAttachmentLimits.ContentTypeMaxLength).IsRequired();
        builder.Property(a => a.BlobName).HasMaxLength(EmailAttachmentLimits.BlobNameMaxLength).IsRequired();
        builder.Property(a => a.FailureReason).HasMaxLength(EmailAttachmentLimits.FailureReasonMaxLength);
        builder.Property(a => a.ContentHash).HasMaxLength(EmailAttachmentLimits.ContentHashLength).IsFixedLength();
        builder.HasOne(a => a.DocumentType)
            .WithMany()
            .HasForeignKey(a => a.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Status)
            .WithMany()
            .HasForeignKey(a => a.StatusId)
            .OnDelete(DeleteBehavior.Restrict);

        // The background upload and its startup recovery look rows up by status.
        builder.HasIndex(a => a.StatusId);

        // Beside the row's own deactivation filter, so a deactivated email's attachments stay hidden with it.
        builder.HasQueryFilter("EmailDeactivation", a => a.Email!.DateDeactivated == null);
    }
}
