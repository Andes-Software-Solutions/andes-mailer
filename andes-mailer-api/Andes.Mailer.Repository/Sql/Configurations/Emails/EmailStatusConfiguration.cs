using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Emails;

/// <summary>
/// Maps <see cref="EmailStatus"/> to <c>[Core.Ref].[EmailStatus]</c>.
/// </summary>
public sealed class EmailStatusConfiguration : BaseEnumEntityConfiguration<EmailStatus, EmailStatuses>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<EmailStatus> builder) =>
        builder.ToTable("EmailStatus", SqlSchemas.CoreRef);
}
