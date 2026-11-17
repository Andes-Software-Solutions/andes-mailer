using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Emails;

/// <summary>
/// Starts a draft email for the signed-in user from one of the team's templates.
/// </summary>
/// <param name="TemplateId">The id of the template whose subject, body, recipients, and attachments the draft starts with.</param>
public sealed record CreateEmailFromTemplateActionDto(Guid TemplateId);

/// <summary>
/// Validates a <see cref="CreateEmailFromTemplateActionDto"/>.
/// </summary>
public sealed class CreateEmailFromTemplateActionDtoValidator : AbstractValidator<CreateEmailFromTemplateActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateEmailFromTemplateActionDtoValidator"/> class.
    /// </summary>
    public CreateEmailFromTemplateActionDtoValidator() =>
        RuleFor(a => a.TemplateId).NotEmpty();
}
