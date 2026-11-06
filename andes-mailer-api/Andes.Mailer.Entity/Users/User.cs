using Andes.Mailer.Entity.Base;

namespace Andes.Mailer.Entity.Users;

/// <summary>
/// Represents a person from the Entra directory who can use the application.
/// </summary>
/// <remarks>
/// <see cref="BaseEntity{TKey}.Id"/> is the person's Entra object id, so a token's <c>oid</c> claim finds the row directly.
/// </remarks>
public sealed class User : BaseModifiedEntity<Guid>
{
    /// <summary>
    /// Gets or sets the first name.
    /// </summary>
    public required string FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name.
    /// </summary>
    public required string LastName { get; set; }

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    /// <value>The address in lowercase; the setter lowercases any value it receives.</value>
    public required string Email
    {
        get;
        set => field = value.ToLowerInvariant();
    }

    /// <summary>
    /// Gets the first and last name joined by a space. It is not stored.
    /// </summary>
    public string FullName => ComposeFullName(FirstName, LastName);

    /// <summary>
    /// Gets or sets the roles the user holds.
    /// </summary>
    public ICollection<UserRole> UserRoles { get; set; } = [];

    #region Public static methods

    /// <summary>
    /// Joins a first and last name, so a query projection and <see cref="FullName"/> compose it the same way.
    /// </summary>
    /// <param name="firstName">The first name.</param>
    /// <param name="lastName">The last name.</param>
    /// <returns>The two names joined by a space, without a leading or trailing space when either is empty.</returns>
    public static string ComposeFullName(string firstName, string lastName) => $"{firstName} {lastName}".Trim();

    #endregion
}
