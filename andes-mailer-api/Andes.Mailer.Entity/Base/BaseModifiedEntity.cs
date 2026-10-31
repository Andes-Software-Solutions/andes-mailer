namespace Andes.Mailer.Entity.Base;

/// <summary>
/// Exposes the modification stamp of <see cref="BaseModifiedEntity{TKey}"/> regardless of key type.
/// </summary>
public interface IBaseModifiedEntity
{
    /// <summary>
    /// Gets or sets the moment the row last changed.
    /// </summary>
    /// <value>A timestamp at offset zero set by the audit interceptor, or <see langword="null"/> if the row has never been updated.</value>
    DateTimeOffset? DateModified { get; set; }
}

/// <summary>
/// Adds a modification stamp and a concurrency token, for rows that users edit.
/// </summary>
/// <typeparam name="TKey">The key type, <see cref="Guid"/> or <see cref="int"/>.</typeparam>
public abstract class BaseModifiedEntity<TKey> : BaseCreatedEntity<TKey>, IBaseModifiedEntity
    where TKey : struct, IEquatable<TKey>
{
    /// <inheritdoc/>
    public DateTimeOffset? DateModified { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token.
    /// </summary>
    /// <value>The row version assigned by the store.</value>
#pragma warning disable CA1819 // EF Core maps a rowversion column to a byte array.
    public byte[] Version { get; set; } = [];
#pragma warning restore CA1819
}
