using Andes.Mailer.Common.Constants.Teams;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Teams;

/// <summary>
/// Creates a team that sends from a service account.
/// </summary>
/// <param name="Name">The name.</param>
/// <param name="Description">The description, or <see langword="null"/> for none.</param>
/// <param name="ServiceAccountEmail">The email address of the directory mailbox the team sends from.</param>
public sealed record CreateTeamActionDto(string Name, string? Description, string ServiceAccountEmail);

/// <summary>
/// Validates a <see cref="CreateTeamActionDto"/>.
/// </summary>
public sealed class CreateTeamActionDtoValidator : AbstractValidator<CreateTeamActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateTeamActionDtoValidator"/> class.
    /// </summary>
    public CreateTeamActionDtoValidator()
    {
        RuleFor(a => a.Name).NotEmpty().MaximumLength(TeamLimits.NameMaxLength);
        RuleFor(a => a.Description).MaximumLength(TeamLimits.DescriptionMaxLength);
        RuleFor(a => a.ServiceAccountEmail)
            .NotEmpty()
            .MaximumLength(TeamLimits.ServiceAccountEmailMaxLength)
            .EmailAddress();
    }
}
