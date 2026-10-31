namespace Andes.Mailer.Entity.Base;

/// <summary>
/// Exposes the creation stamp of <see cref="BaseCreatedEntity{TKey}"/> regardless of key type.
/// </summary>
public interface IBaseCreatedEntity
{
    /// <summary>
    /// Gets or sets the moment the row was created.
    /// </summary>
    /// <value>A timestamp at offset zero, set once by the audit interceptor.</value>
    DateTimeOffset DateCreated { get; set; }
}

/// <summary>
/// Adds a creation stamp, for append-only rows.
/// </summary>
/// <typeparam name="TKey">The key type, <see cref="Guid"/> or <see cref="int"/>.</typeparam>
public abstract class BaseCreatedEntity<TKey> : BaseEntity<TKey>, IBaseCreatedEntity
    where TKey : struct, IEquatable<TKey>
{
    /// <inheritdoc/>
    public DateTimeOffset DateCreated { get; set; }
}
