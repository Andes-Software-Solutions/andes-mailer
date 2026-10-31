using Andes.Mailer.Entity.Base;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Base;

/// <summary>
/// Maps the members of <see cref="BaseEntity{TKey}"/>.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TKey">The key type.</typeparam>
public abstract class BaseEntityConfiguration<T, TKey> : IEntityTypeConfiguration<T>
    where T : BaseEntity<TKey>
    where TKey : struct, IEquatable<TKey>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<T> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        ConfigureBase(builder);
        ConfigureEntity(builder);
    }

    /// <summary>
    /// Maps the base members.
    /// </summary>
    /// <param name="builder">The entity builder.</param>
    protected virtual void ConfigureBase(EntityTypeBuilder<T> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.HasQueryFilter("Deactivation", e => e.DateDeactivated == null);
        builder.HasIndex(e => e.DateDeactivated);
    }

    /// <summary>
    /// Maps the members of the derived entity.
    /// </summary>
    /// <param name="builder">The entity builder.</param>
    protected abstract void ConfigureEntity(EntityTypeBuilder<T> builder);
}
