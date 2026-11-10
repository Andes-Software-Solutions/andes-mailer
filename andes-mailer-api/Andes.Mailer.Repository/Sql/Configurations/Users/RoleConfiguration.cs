using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Users;

/// <summary>
/// Maps <see cref="Role"/> to <c>[Core.Ref].[Role]</c>.
/// </summary>
public sealed class RoleConfiguration : BaseEnumEntityConfiguration<Role, Roles>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<Role> builder) =>
        builder.ToTable("Role", SqlSchemas.CoreRef);
}
