using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Emails;

/// <summary>
/// Starts a draft email for the signed-in user.
/// </summary>
/// <param name="Subject">The subject; may be empty while composing.</param>
/// <param name="BodyHtml">The body as HTML; the API strips anything outside the editor's formatting before storing it.</param>
/// <param name="Recipients">The recipients, in the order the sender entered them.</param>
public sealed record CreateEmailActionDto(string Subject, string BodyHtml, IReadOnlyList<EmailRecipientActionDto> Recipients);

/// <summary>
/// Validates a <see cref="CreateEmailActionDto"/>.
/// </summary>
public sealed class CreateEmailActionDtoValidator : AbstractValidator<CreateEmailActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateEmailActionDtoValidator"/> class.
    /// </summary>
    public CreateEmailActionDtoValidator()
    {
        RuleFor(a => a.Subject).ValidSubject();
        RuleFor(a => a.BodyHtml).ValidBodyHtml();
        RuleFor(a => a.Recipients).Cascade(CascadeMode.Stop).ValidRecipients();
        RuleForEach(a => a.Recipients).SetValidator(new EmailRecipientActionDtoValidator()).When(a => a.Recipients is not null);
    }
}
