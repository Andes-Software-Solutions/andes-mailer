namespace Andes.Mailer.Service.Users.Models;

/// <summary>
/// Describes an account found in the Entra directory.
/// </summary>
/// <param name="Id">The Entra object id.</param>
/// <param name="FirstName">The given name, or the best substitute the directory offers.</param>
/// <param name="LastName">The surname, or an empty string if the directory has none.</param>
/// <param name="Email">The email address the account was found by, in lowercase.</param>
public sealed record DirectoryUser(Guid Id, string FirstName, string LastName, string Email);
