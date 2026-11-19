using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Emails;

/// <summary>
/// Maps <see cref="DeliveryStatus"/> to <c>[Core.Ref].[DeliveryStatus]</c>.
/// </summary>
public sealed class DeliveryStatusConfiguration : BaseEnumEntityConfiguration<DeliveryStatus, DeliveryStatuses>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<DeliveryStatus> builder) =>
        builder.ToTable("DeliveryStatus", SqlSchemas.CoreRef);
}
