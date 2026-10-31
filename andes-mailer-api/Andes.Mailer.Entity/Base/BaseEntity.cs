namespace Andes.Mailer.Entity.Base;

/// <summary>
/// Exposes the deactivation stamp of <see cref="BaseEntity{TKey}"/> regardless of key type.
/// </summary>
public interface IBaseEntity
{
    /// <summary>
    /// Gets or sets the moment the row was deactivated.
    /// </summary>
    /// <value>A timestamp at offset zero, or <see langword="null"/> while the row is active.</value>
    DateTimeOffset? DateDeactivated { get; set; }
}

/// <summary>
/// Provides the key and deactivation stamp every table shares.
/// </summary>
/// <typeparam name="TKey">The key type, <see cref="Guid"/> or <see cref="int"/>.</typeparam>
public abstract class BaseEntity<TKey> : IBaseEntity
    where TKey : struct, IEquatable<TKey>
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public TKey Id { get; set; }

    /// <inheritdoc/>
    public DateTimeOffset? DateDeactivated { get; set; }
}
