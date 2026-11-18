using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Common.Enums;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Emails;

/// <summary>
/// Addresses an email to one recipient.
/// </summary>
/// <param name="Address">The recipient's email address, inside or outside the directory.</param>
/// <param name="Type">One of the enumeration values that specifies the header the recipient is addressed in.</param>
public sealed record EmailRecipientActionDto(string Address, RecipientTypes Type);

/// <summary>
/// Validates an <see cref="EmailRecipientActionDto"/>.
/// </summary>
public sealed class EmailRecipientActionDtoValidator : AbstractValidator<EmailRecipientActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmailRecipientActionDtoValidator"/> class.
    /// </summary>
    public EmailRecipientActionDtoValidator()
    {
        RuleFor(r => r.Address).PlainAddress(EmailRecipientLimits.AddressMaxLength);
        RuleFor(r => r.Type).IsInEnum();
    }
}
