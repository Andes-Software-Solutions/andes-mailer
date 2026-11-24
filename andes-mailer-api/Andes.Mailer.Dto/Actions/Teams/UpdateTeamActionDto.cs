using System.Buffers.Text;

using Andes.Mailer.Common.Constants.Base;
using Andes.Mailer.Common.Constants.Teams;
using Andes.Mailer.Common.Enums;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Teams;

/// <summary>
/// Replaces a team's details and settings.
/// </summary>
/// <param name="Name">The new name.</param>
/// <param name="Description">The new description, or <see langword="null"/> for none.</param>
/// <param name="ServiceAccountEmail">The email address of the directory mailbox the team sends from; only a global administrator may change it.</param>
/// <param name="AvatarColor">One of the enumeration values that specifies the colour of the team's avatar.</param>
/// <param name="DefaultSignature">The HTML signature added to new emails, or <see langword="null"/> for none; the API sanitizes it.</param>
/// <param name="ConfirmRecipientsAbove">The number of recipients above which sending asks for confirmation.</param>
/// <param name="Version">The row version the caller last read, as base64; the update fails if the team changed since.</param>
public sealed record UpdateTeamActionDto(
    string Name,
    string? Description,
    string ServiceAccountEmail,
    TeamColors AvatarColor,
    string? DefaultSignature,
    int ConfirmRecipientsAbove,
    string Version);

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
        RuleFor(a => a.AvatarColor).IsInEnum();
        RuleFor(a => a.DefaultSignature).MaximumLength(TeamLimits.DefaultSignatureMaxLength);
        RuleFor(a => a.ConfirmRecipientsAbove).InclusiveBetween(TeamLimits.ConfirmRecipientsAboveMin, TeamLimits.ConfirmRecipientsAboveMax);

        // The decoded length too: well-formed base64 of the wrong size would otherwise reach the store and come back as a 409.
        RuleFor(a => a.Version)
            .NotEmpty()
            .Must(v => Base64.IsValid(v, out var length) && length == BaseModifiedEntityLimits.VersionLength)
            .WithMessage("'Version' must be the base64 row version returned by the API.");
    }
}
