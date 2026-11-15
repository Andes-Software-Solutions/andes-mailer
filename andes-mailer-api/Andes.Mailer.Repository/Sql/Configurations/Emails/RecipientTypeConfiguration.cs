using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Emails;

/// <summary>
/// Maps <see cref="RecipientType"/> to <c>[Core.Ref].[RecipientType]</c>.
/// </summary>
public sealed class RecipientTypeConfiguration : BaseEnumEntityConfiguration<RecipientType, RecipientTypes>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<RecipientType> builder) =>
        builder.ToTable("RecipientType", SqlSchemas.CoreRef);
}
