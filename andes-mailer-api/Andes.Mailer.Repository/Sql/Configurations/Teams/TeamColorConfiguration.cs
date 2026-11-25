using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Teams;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Teams;

/// <summary>
/// Maps <see cref="TeamColor"/> to <c>[Core.Ref].[TeamColor]</c>.
/// </summary>
public sealed class TeamColorConfiguration : BaseEnumEntityConfiguration<TeamColor, TeamColors>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<TeamColor> builder) =>
        builder.ToTable("TeamColor", SqlSchemas.CoreRef);
}
