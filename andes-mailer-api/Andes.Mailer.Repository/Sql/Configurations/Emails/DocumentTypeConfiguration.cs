using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Emails;

/// <summary>
/// Maps <see cref="DocumentType"/> to <c>[Core.Ref].[DocumentType]</c>.
/// </summary>
public sealed class DocumentTypeConfiguration : BaseEnumEntityConfiguration<DocumentType, DocumentTypes>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<DocumentType> builder) =>
        builder.ToTable("DocumentType", SqlSchemas.CoreRef);
}
