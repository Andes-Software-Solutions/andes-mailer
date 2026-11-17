using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Dto.Emails.Templates;

/// <summary>
/// Describes one recipient of an email template.
/// </summary>
/// <param name="Address">The email address, in lowercase.</param>
/// <param name="Type">One of the enumeration values that specifies the header the recipient is addressed in.</param>
public sealed record EmailTemplateRecipientDto(string Address, RecipientTypes Type);
