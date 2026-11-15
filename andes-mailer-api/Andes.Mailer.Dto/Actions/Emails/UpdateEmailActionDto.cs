using System.Buffers.Text;

using Andes.Mailer.Common.Constants.Base;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Emails;

/// <summary>
/// Replaces the content of a draft email.
/// </summary>
/// <param name="Subject">The subject; may be empty while composing.</param>
/// <param name="BodyHtml">The body as HTML; the API strips anything outside the editor's formatting before storing it.</param>
/// <param name="Recipients">The complete recipient list, in the order the sender entered it; recipients left out are removed.</param>
/// <param name="Version">The row version the caller last read, as base64; the update fails if the email changed since.</param>
public sealed record UpdateEmailActionDto(string Subject, string BodyHtml, IReadOnlyList<EmailRecipientActionDto> Recipients, string Version);

/// <summary>
/// Validates an <see cref="UpdateEmailActionDto"/>.
/// </summary>
public sealed class UpdateEmailActionDtoValidator : AbstractValidator<UpdateEmailActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateEmailActionDtoValidator"/> class.
    /// </summary>
    public UpdateEmailActionDtoValidator()
    {
        RuleFor(a => a.Subject).ValidSubject();
        RuleFor(a => a.BodyHtml).ValidBodyHtml();
        RuleFor(a => a.Recipients).Cascade(CascadeMode.Stop).ValidRecipients();
        RuleForEach(a => a.Recipients).SetValidator(new EmailRecipientActionDtoValidator()).When(a => a.Recipients is not null);

        // The decoded length too: well-formed base64 of the wrong size would otherwise reach the store and come back as a 409.
        RuleFor(a => a.Version)
            .NotEmpty()
            .Must(v => Base64.IsValid(v, out var length) && length == BaseModifiedEntityLimits.VersionLength)
            .WithMessage("'Version' must be the base64 row version returned by the API.");
    }
}
