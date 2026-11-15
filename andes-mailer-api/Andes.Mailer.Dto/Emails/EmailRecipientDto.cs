using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Dto.Emails;

/// <summary>
/// Describes one recipient of an email.
/// </summary>
/// <param name="Address">The email address, in lowercase.</param>
/// <param name="Type">One of the enumeration values that specifies the header the recipient is addressed in.</param>
/// <param name="UserId">The Entra object id of the directory account that owns the address, or <see langword="null"/> for an address outside the directory.</param>
/// <param name="DisplayName">The directory account's name, or <see langword="null"/> for an address outside the directory.</param>
public sealed record EmailRecipientDto(string Address, RecipientTypes Type, Guid? UserId, string? DisplayName);
