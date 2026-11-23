using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Dto.Users;

/// <summary>
/// Describes the signed-in user and what they may do in the application.
/// </summary>
/// <param name="Id">The user's Entra object id.</param>
/// <param name="Name">The user's display name.</param>
/// <param name="Email">The user's email address, or <see langword="null"/> if neither the user row nor the token carries one.</param>
/// <param name="IsActive"><see langword="true"/> if the account is an active user of the application; otherwise, <see langword="false"/>, and every list is empty.</param>
/// <param name="Roles">The global roles the user holds, ordered by value.</param>
/// <param name="Permissions">The permissions the user's global roles grant, ordered by value.</param>
/// <param name="Teams">The teams the user can see, ordered by name, with what the user may do in each.</param>
public sealed record CurrentUserDto(
    Guid Id,
    string Name,
    string? Email,
    bool IsActive,
    IReadOnlyList<Roles> Roles,
    IReadOnlyList<Permissions> Permissions,
    IReadOnlyList<CurrentUserTeamDto> Teams);
