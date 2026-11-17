using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Entity.Emails.Templates;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Emails.Templates;

/// <summary>
/// Maps <see cref="EmailTemplateAttachment"/> to <c>[Core].[EmailTemplateAttachment]</c>.
/// </summary>
public sealed class EmailTemplateAttachmentConfiguration : BaseModifiedEntityConfiguration<EmailTemplateAttachment, Guid>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<EmailTemplateAttachment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EmailTemplateAttachment", SqlSchemas.Core);
        builder.Property(a => a.FileName).HasMaxLength(EmailAttachmentLimits.FileNameMaxLength).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(EmailAttachmentLimits.ContentTypeMaxLength).IsRequired();
        builder.Property(a => a.BlobName).HasMaxLength(EmailAttachmentLimits.BlobNameMaxLength).IsRequired();
        builder.Property(a => a.FailureReason).HasMaxLength(EmailAttachmentLimits.FailureReasonMaxLength);
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

        // Beside the row's own deactivation filter, so a deactivated template's attachments stay hidden with it.
        builder.HasQueryFilter("TemplateDeactivation", a => a.Template!.DateDeactivated == null);
    }
}
