using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Dto.Teams;

/// <summary>
/// Describes a team and its settings.
/// </summary>
/// <param name="Id">The team's id.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description, or <see langword="null"/> if there is none.</param>
/// <param name="ServiceAccountEmail">The email address of the mailbox the team sends from, in lowercase.</param>
/// <param name="AvatarColor">One of the enumeration values that specifies the colour of the team's avatar.</param>
/// <param name="DefaultSignature">The sanitized HTML signature added to new emails, or <see langword="null"/> if there is none.</param>
/// <param name="ConfirmRecipientsAbove">The number of recipients above which sending asks for confirmation.</param>
/// <param name="MemberCount">The number of active members.</param>
/// <param name="AdministratorCount">The number of active members who are team administrators.</param>
/// <param name="DateCreated">The moment the team was created.</param>
/// <param name="DateModified">The moment the team last changed, or <see langword="null"/> if never.</param>
/// <param name="Version">The row version as base64; send it back to update the team.</param>
public sealed record TeamDto(
    Guid Id,
    string Name,
    string? Description,
    string ServiceAccountEmail,
    TeamColors AvatarColor,
    string? DefaultSignature,
    int ConfirmRecipientsAbove,
    int MemberCount,
    int AdministratorCount,
    DateTimeOffset DateCreated,
    DateTimeOffset? DateModified,
    string Version);
