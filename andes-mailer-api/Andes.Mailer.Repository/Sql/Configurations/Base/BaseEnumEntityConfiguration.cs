using System.ComponentModel;
using System.Reflection;

using Andes.Mailer.Common.Constants.Base;
using Andes.Mailer.Entity.Base;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Base;

/// <summary>
/// Maps the members of <see cref="BaseEnumEntity{TEnum}"/> and seeds one row per enum member.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
/// <typeparam name="TEnum">The enum whose members are the rows.</typeparam>
public abstract class BaseEnumEntityConfiguration<TEntity, TEnum> : IEntityTypeConfiguration<TEntity>
    where TEntity : BaseEnumEntity<TEnum>
    where TEnum : struct, Enum
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<TEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Name).HasMaxLength(BaseEnumEntityLimits.NameMaxLength).IsRequired();
        builder.HasIndex(e => e.Name).IsUnique();
        builder.Property(e => e.Description).HasMaxLength(BaseEnumEntityLimits.DescriptionMaxLength);

        // Anonymous objects: a type with a required member cannot satisfy a new() constraint.
        builder.HasData(Enum.GetValues<TEnum>().Select(value => new
        {
            Id = value,
            Name = value.ToString(),
            Description = DescriptionOf(value),
        }));
        ConfigureEntity(builder);
    }

    /// <summary>
    /// Maps the members of the derived entity.
    /// </summary>
    /// <param name="builder">The entity builder.</param>
    protected virtual void ConfigureEntity(EntityTypeBuilder<TEntity> builder)
    {
    }

    #region Private methods

    private static string? DescriptionOf(TEnum value) =>
        typeof(TEnum).GetField(value.ToString())?.GetCustomAttribute<DescriptionAttribute>()?.Description;

    #endregion
}
