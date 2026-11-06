using Andes.Mailer.Common.Constants.Users;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Users;

/// <summary>
/// Adds a user from the Entra directory.
/// </summary>
/// <param name="Email">The email address of the directory account; the remaining details come from the directory.</param>
public sealed record CreateUserActionDto(string Email);

/// <summary>
/// Validates a <see cref="CreateUserActionDto"/>.
/// </summary>
public sealed class CreateUserActionDtoValidator : AbstractValidator<CreateUserActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateUserActionDtoValidator"/> class.
    /// </summary>
    public CreateUserActionDtoValidator() =>
        RuleFor(a => a.Email)
            .NotEmpty()
            .MaximumLength(UserLimits.EmailMaxLength)
            .EmailAddress();
}
