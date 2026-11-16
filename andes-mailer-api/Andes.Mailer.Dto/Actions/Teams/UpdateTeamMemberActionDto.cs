using System.Buffers.Text;

using Andes.Mailer.Common.Constants.Base;
using Andes.Mailer.Common.Constants.Users;
using Andes.Mailer.Common.Enums;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Teams;

/// <summary>
/// Changes the role a member holds in a team.
/// </summary>
/// <param name="Role">One of the team roles: <see cref="Roles.TeamAdministrator"/> or <see cref="Roles.TeamMember"/>.</param>
/// <param name="Version">The membership's row version the caller last read, as base64; the update fails if it changed since.</param>
public sealed record UpdateTeamMemberActionDto(Roles Role, string Version);

/// <summary>
/// Validates an <see cref="UpdateTeamMemberActionDto"/>.
/// </summary>
public sealed class UpdateTeamMemberActionDtoValidator : AbstractValidator<UpdateTeamMemberActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTeamMemberActionDtoValidator"/> class.
    /// </summary>
    public UpdateTeamMemberActionDtoValidator()
    {
        RuleFor(a => a.Role)
            .Must(RoleScopes.TeamRoles.Contains)
            .WithMessage("'Role' must be TeamAdministrator or TeamMember.");

        // The decoded length too: well-formed base64 of the wrong size would otherwise reach the store and come back as a 409.
        RuleFor(a => a.Version)
            .NotEmpty()
            .Must(v => Base64.IsValid(v, out var length) && length == BaseModifiedEntityLimits.VersionLength)
            .WithMessage("'Version' must be the base64 row version returned by the API.");
    }
}
