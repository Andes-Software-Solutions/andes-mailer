using System.Buffers.Text;

using Andes.Mailer.Common.Constants.Base;
using Andes.Mailer.Common.Constants.Teams;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Teams;

/// <summary>
/// Replaces a team's details.
/// </summary>
/// <param name="Name">The new name.</param>
/// <param name="Description">The new description, or <see langword="null"/> for none.</param>
/// <param name="ServiceAccountEmail">The email address of the directory mailbox the team sends from.</param>
/// <param name="Version">The row version the caller last read, as base64; the update fails if the team changed since.</param>
public sealed record UpdateTeamActionDto(string Name, string? Description, string ServiceAccountEmail, string Version);

/// <summary>
/// Validates an <see cref="UpdateTeamActionDto"/>.
/// </summary>
public sealed class UpdateTeamActionDtoValidator : AbstractValidator<UpdateTeamActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateTeamActionDtoValidator"/> class.
    /// </summary>
    public UpdateTeamActionDtoValidator()
    {
        RuleFor(a => a.Name).NotEmpty().MaximumLength(TeamLimits.NameMaxLength);
        RuleFor(a => a.Description).MaximumLength(TeamLimits.DescriptionMaxLength);
        RuleFor(a => a.ServiceAccountEmail)
            .NotEmpty()
            .MaximumLength(TeamLimits.ServiceAccountEmailMaxLength)
            .EmailAddress();

        // The decoded length too: well-formed base64 of the wrong size would otherwise reach the store and come back as a 409.
        RuleFor(a => a.Version)
            .NotEmpty()
            .Must(v => Base64.IsValid(v, out var length) && length == BaseModifiedEntityLimits.VersionLength)
            .WithMessage("'Version' must be the base64 row version returned by the API.");
    }
}
