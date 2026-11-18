namespace Andes.Mailer.Dto.ContactGroups;

/// <summary>
/// Describes a contact group in a list, without its members.
/// </summary>
/// <param name="Id">The group's id.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description, or <see langword="null"/> if there is none.</param>
/// <param name="MemberCount">The number of members.</param>
public sealed record ContactGroupSummaryDto(Guid Id, string Name, string? Description, int MemberCount);
