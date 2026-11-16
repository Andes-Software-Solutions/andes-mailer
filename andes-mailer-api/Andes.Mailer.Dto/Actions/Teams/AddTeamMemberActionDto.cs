using Andes.Mailer.Common.Constants.Users;
using Andes.Mailer.Common.Enums;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Teams;

/// <summary>
/// Adds a person from the Entra directory to a team, adding them as a user first if they are not one yet.
/// </summary>
/// <param name="Email">The email address of the directory account.</param>
/// <param name="Role">One of the team roles: <see cref="Roles.TeamAdministrator"/> or <see cref="Roles.TeamMember"/>.</param>
public sealed record AddTeamMemberActionDto(string Email, Roles Role);

/// <summary>
/// Validates an <see cref="AddTeamMemberActionDto"/>.
/// </summary>
public sealed class AddTeamMemberActionDtoValidator : AbstractValidator<AddTeamMemberActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AddTeamMemberActionDtoValidator"/> class.
    /// </summary>
    public AddTeamMemberActionDtoValidator()
    {
        RuleFor(a => a.Email)
            .NotEmpty()
            .MaximumLength(UserLimits.EmailMaxLength)
            .EmailAddress();
        RuleFor(a => a.Role)
            .Must(RoleScopes.TeamRoles.Contains)
            .WithMessage("'Role' must be TeamAdministrator or TeamMember.");
    }
}
