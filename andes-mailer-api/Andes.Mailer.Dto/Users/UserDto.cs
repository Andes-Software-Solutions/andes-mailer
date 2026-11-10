namespace Andes.Mailer.Dto.Users;

/// <summary>
/// Describes a user.
/// </summary>
/// <param name="Id">The user's Entra object id.</param>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
/// <param name="FullName">The first and last name joined by a space.</param>
/// <param name="Email">The email address, in lowercase.</param>
/// <param name="DateCreated">The moment the user was added.</param>
/// <param name="DateModified">The moment the user last changed, or <see langword="null"/> if never.</param>
/// <param name="Version">The row version as base64; send it back to update the user.</param>
public sealed record UserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    DateTimeOffset DateCreated,
    DateTimeOffset? DateModified,
    string Version);
