using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Dto.Teams;

/// <summary>
/// Describes a team in a list.
/// </summary>
/// <param name="Id">The team's id.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description, or <see langword="null"/> if there is none.</param>
/// <param name="ServiceAccountEmail">The email address of the mailbox the team sends from, in lowercase.</param>
/// <param name="MemberCount">The number of active members.</param>
/// <param name="Role">The role the signed-in user holds in the team, or <see langword="null"/> if they are not a member.</param>
public sealed record TeamSummaryDto(
    Guid Id,
    string Name,
    string? Description,
    string ServiceAccountEmail,
    int MemberCount,
    Roles? Role);
