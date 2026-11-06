namespace Andes.Mailer.Entity.Base;

/// <summary>
/// Provides the members of a reference table whose rows are the members of an enum.
/// </summary>
/// <typeparam name="TEnum">The enum whose members are the rows.</typeparam>
public abstract class BaseEnumEntity<TEnum>
    where TEnum : struct, Enum
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    /// <value>The enum member, stored as its integer value.</value>
    public TEnum Id { get; set; }

    /// <summary>
    /// Gets or sets the member name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the member description.
    /// </summary>
    /// <value>The text of the member's <see cref="System.ComponentModel.DescriptionAttribute"/>, or <see langword="null"/> if it has none.</value>
    public string? Description { get; set; }
}
