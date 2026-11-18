namespace Andes.Mailer.Dto.ContactGroups;

/// <summary>
/// Describes one address of a contact group.
/// </summary>
/// <param name="Address">The email address, in lowercase.</param>
/// <param name="UserId">The Entra object id of the directory account that owns the address, or <see langword="null"/> for an address outside the directory.</param>
/// <param name="DisplayName">The directory account's display name, or <see langword="null"/> for an address outside the directory.</param>
public sealed record ContactGroupMemberDto(string Address, Guid? UserId, string? DisplayName);
