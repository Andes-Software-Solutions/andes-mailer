using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Emails.Templates;

/// <summary>
/// Creates a template for a team as a copy of another of its templates.
/// </summary>
/// <param name="Name">
/// The copy's name; unique among the team's active templates. When <see langword="null"/>, the API names it after the source, as
/// <c>"{Name} (copy)"</c> or the next free <c>"{Name} (copy N)"</c>.
/// </param>
public sealed record DuplicateEmailTemplateActionDto(string? Name);

/// <summary>
/// Validates a <see cref="DuplicateEmailTemplateActionDto"/>.
/// </summary>
public sealed class DuplicateEmailTemplateActionDtoValidator : AbstractValidator<DuplicateEmailTemplateActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DuplicateEmailTemplateActionDtoValidator"/> class.
    /// </summary>
    public DuplicateEmailTemplateActionDtoValidator() =>
        RuleFor(a => a.Name!).ValidTemplateName().When(a => a.Name is not null);
}
