using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Dto.Teams;

/// <summary>
/// Describes a member of a team.
/// </summary>
/// <param name="UserId">The member's Entra object id.</param>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
/// <param name="FullName">The first and last name joined by a space.</param>
/// <param name="Email">The email address, in lowercase.</param>
/// <param name="Role">One of the enumeration values that specifies the role the member holds in the team.</param>
/// <param name="DateCreated">The moment the member was added to the team.</param>
/// <param name="DateModified">The moment the membership last changed, or <see langword="null"/> if never.</param>
/// <param name="Version">The membership's row version as base64; send it back to change the member's role.</param>
public sealed record TeamMemberDto(
    Guid UserId,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    Roles Role,
    DateTimeOffset DateCreated,
    DateTimeOffset? DateModified,
    string Version);
