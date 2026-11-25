using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Dto.Users;

/// <summary>
/// Describes a team the signed-in user can see and what they may do in it.
/// </summary>
/// <param name="Id">The team's id.</param>
/// <param name="Name">The team's name.</param>
/// <param name="AvatarColor">One of the enumeration values that specifies the colour of the team's avatar.</param>
/// <param name="Role">The role the user holds in the team, or <see langword="null"/> if they see it through a global role only.</param>
/// <param name="Permissions">The permissions the user holds in the team, through their team role or a global role, ordered by value.</param>
public sealed record CurrentUserTeamDto(Guid Id, string Name, TeamColors AvatarColor, Roles? Role, IReadOnlyList<Permissions> Permissions);
