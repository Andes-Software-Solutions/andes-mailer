using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Emails;

/// <summary>
/// Changes an email's sending state, such as returning a scheduled email to a draft or retrying its failed recipients.
/// </summary>
/// <param name="Version">The row version the caller last read, as base64; the change fails if the email changed since.</param>
public sealed record EmailVersionActionDto(string Version);

/// <summary>
/// Validates an <see cref="EmailVersionActionDto"/>.
/// </summary>
public sealed class EmailVersionActionDtoValidator : AbstractValidator<EmailVersionActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmailVersionActionDtoValidator"/> class.
    /// </summary>
    public EmailVersionActionDtoValidator() =>
        RuleFor(a => a.Version).ValidVersion();
}
