using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Emails;

/// <summary>
/// Maps <see cref="AttachmentStatus"/> to <c>[Core.Ref].[AttachmentStatus]</c>.
/// </summary>
public sealed class AttachmentStatusConfiguration : BaseEnumEntityConfiguration<AttachmentStatus, AttachmentStatuses>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<AttachmentStatus> builder) =>
        builder.ToTable("AttachmentStatus", SqlSchemas.CoreRef);
}
