namespace Andes.Mailer.Dto.Users;

/// <summary>
/// Describes a person found in the Entra directory.
/// </summary>
/// <param name="Id">The Entra object id; it is also the id of the matching user of this application, if there is one.</param>
/// <param name="FirstName">The given name, or the best substitute the directory offers.</param>
/// <param name="LastName">The surname, or an empty string if the directory has none.</param>
/// <param name="FullName">The first and last names joined, for display.</param>
/// <param name="Email">The mail address, or the sign-in name when the account has none, in lowercase.</param>
public sealed record DirectoryUserDto(Guid Id, string FirstName, string LastName, string FullName, string Email);
