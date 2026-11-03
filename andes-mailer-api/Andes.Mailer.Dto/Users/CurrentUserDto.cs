namespace Andes.Mailer.Dto.Users;

/// <summary>
/// Describes the signed-in user.
/// </summary>
/// <param name="Id">The user's Entra object id.</param>
/// <param name="Name">The user's display name.</param>
public sealed record CurrentUserDto(string Id, string Name);
