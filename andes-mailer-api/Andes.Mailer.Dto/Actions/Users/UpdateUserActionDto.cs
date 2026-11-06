using System.Buffers.Text;

using Andes.Mailer.Common.Constants.Users;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Users;

/// <summary>
/// Changes a user's name.
/// </summary>
/// <param name="FirstName">The new first name.</param>
/// <param name="LastName">The new last name.</param>
/// <param name="Version">The row version the caller last read, as base64; the update fails if the user changed since.</param>
public sealed record UpdateUserActionDto(string FirstName, string LastName, string Version);

/// <summary>
/// Validates an <see cref="UpdateUserActionDto"/>.
/// </summary>
public sealed class UpdateUserActionDtoValidator : AbstractValidator<UpdateUserActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserActionDtoValidator"/> class.
    /// </summary>
    public UpdateUserActionDtoValidator()
    {
        RuleFor(a => a.FirstName).NotEmpty().MaximumLength(UserLimits.FirstNameMaxLength);

        // NotNull, not NotEmpty: directory accounts may have no surname, but a missing field must not reach the service.
        RuleFor(a => a.LastName).NotNull().MaximumLength(UserLimits.LastNameMaxLength);
        RuleFor(a => a.Version)
            .NotEmpty()
            .Must(v => Base64.IsValid(v))
            .WithMessage("'Version' must be the base64 row version returned by the API.");
    }
}
