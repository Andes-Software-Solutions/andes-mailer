using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Emails.Templates;

/// <summary>
/// Creates an email template for a team.
/// </summary>
/// <param name="Name">The name the team picks the template by; unique among the team's active templates.</param>
/// <param name="Subject">The subject; may be empty.</param>
/// <param name="BodyHtml">The body as HTML; the API strips anything outside the editor's formatting before storing it.</param>
/// <param name="Recipients">The recipients, in the order they were entered.</param>
public sealed record CreateEmailTemplateActionDto(string Name, string Subject, string BodyHtml, IReadOnlyList<EmailRecipientActionDto> Recipients);

/// <summary>
/// Validates a <see cref="CreateEmailTemplateActionDto"/>.
/// </summary>
public sealed class CreateEmailTemplateActionDtoValidator : AbstractValidator<CreateEmailTemplateActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateEmailTemplateActionDtoValidator"/> class.
    /// </summary>
    public CreateEmailTemplateActionDtoValidator()
    {
        RuleFor(a => a.Name).ValidTemplateName();
        RuleFor(a => a.Subject).ValidSubject();
        RuleFor(a => a.BodyHtml).ValidBodyHtml();
        RuleFor(a => a.Recipients).Cascade(CascadeMode.Stop).ValidRecipients();
        RuleForEach(a => a.Recipients).SetValidator(new EmailRecipientActionDtoValidator()).When(a => a.Recipients is not null);
    }
}
