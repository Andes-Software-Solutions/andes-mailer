using Andes.Mailer.Common.Constants.Teams;
using Andes.Mailer.Common.Enums;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Teams;

/// <summary>
/// Creates a team that sends from a service account.
/// </summary>
/// <param name="Name">The name.</param>
/// <param name="Description">The description, or <see langword="null"/> for none.</param>
/// <param name="ServiceAccountEmail">The email address of the directory mailbox the team sends from.</param>
/// <param name="AvatarColor">One of the enumeration values that specifies the colour of the team's avatar; <see cref="TeamColors.Mint"/> when omitted.</param>
public sealed record CreateTeamActionDto(string Name, string? Description, string ServiceAccountEmail, TeamColors AvatarColor = TeamColors.Mint);

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
        RuleFor(a => a.AvatarColor).IsInEnum();
    }
}
