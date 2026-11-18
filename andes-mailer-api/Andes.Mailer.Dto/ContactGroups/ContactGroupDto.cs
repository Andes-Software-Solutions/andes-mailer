namespace Andes.Mailer.Dto.ContactGroups;

/// <summary>
/// Describes a contact group with its members.
/// </summary>
/// <param name="Id">The group's id.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description, or <see langword="null"/> if there is none.</param>
/// <param name="Members">The members, in the order they were entered.</param>
/// <param name="DateCreated">The moment the group was created.</param>
/// <param name="DateModified">The moment the group last changed, or <see langword="null"/> if never.</param>
/// <param name="Version">The row version as base64; send it back to update the group.</param>
public sealed record ContactGroupDto(
    Guid Id,
    string Name,
    string? Description,
    IReadOnlyList<ContactGroupMemberDto> Members,
    DateTimeOffset DateCreated,
    DateTimeOffset? DateModified,
    string Version);
