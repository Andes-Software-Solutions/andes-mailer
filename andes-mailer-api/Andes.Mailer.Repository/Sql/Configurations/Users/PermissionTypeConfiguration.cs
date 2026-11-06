using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Users;

/// <summary>
/// Maps <see cref="PermissionType"/> to <c>[Core.Ref].[Permission]</c>.
/// </summary>
public sealed class PermissionTypeConfiguration : BaseEnumEntityConfiguration<PermissionType, Permissions>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<PermissionType> builder) =>
        builder.ToTable("Permission", SqlSchemas.CoreRef);
}
