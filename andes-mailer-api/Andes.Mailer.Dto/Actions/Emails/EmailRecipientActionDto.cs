using System.Net.Mail;

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
        // FluentValidation's EmailAddress() only checks for an inner '@'; MailAddress also refuses whitespace, control characters, and display names,
        // which would otherwise reach the directory filter and, later, the message headers.
        RuleFor(r => r.Address)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(EmailRecipientLimits.AddressMaxLength)
            .Must(IsPlainAddress)
            .WithMessage("'Address' must be a single email address with no name or spaces.");
        RuleFor(r => r.Type).IsInEnum();
    }

    #region Private methods

    private static bool IsPlainAddress(string address)
    {
        var trimmed = address.Trim();

        return MailAddress.TryCreate(trimmed, out var parsed)
            && string.Equals(parsed.Address, trimmed, StringComparison.Ordinal)
            && !trimmed.Any(char.IsWhiteSpace);
    }

    #endregion
}
