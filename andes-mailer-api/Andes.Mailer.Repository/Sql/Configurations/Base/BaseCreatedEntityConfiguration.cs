using Andes.Mailer.Entity.Base;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Base;

/// <summary>
/// Maps the members of <see cref="BaseCreatedEntity{TKey}"/>.
/// </summary>
/// <typeparam name="T">The entity type.</typeparam>
/// <typeparam name="TKey">The key type.</typeparam>
public abstract class BaseCreatedEntityConfiguration<T, TKey> : BaseEntityConfiguration<T, TKey>
    where T : BaseCreatedEntity<TKey>
    where TKey : struct, IEquatable<TKey>
{
    /// <inheritdoc/>
    protected override void ConfigureBase(EntityTypeBuilder<T> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        base.ConfigureBase(builder);
        builder.Property(e => e.DateCreated).IsRequired();
    }
}
