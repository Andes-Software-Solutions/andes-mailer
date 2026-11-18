using FluentValidation;

namespace Andes.Mailer.Dto.Actions.ContactGroups;

/// <summary>
/// Creates a contact group in a team.
/// </summary>
/// <param name="Name">The name, unique among the team's active groups.</param>
/// <param name="Description">The description, or <see langword="null"/> for none.</param>
/// <param name="Members">The members' addresses, in the order they are listed.</param>
public sealed record CreateContactGroupActionDto(string Name, string? Description, IReadOnlyList<ContactGroupMemberActionDto> Members);

/// <summary>
/// Validates a <see cref="CreateContactGroupActionDto"/>.
/// </summary>
public sealed class CreateContactGroupActionDtoValidator : AbstractValidator<CreateContactGroupActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreateContactGroupActionDtoValidator"/> class.
    /// </summary>
    public CreateContactGroupActionDtoValidator()
    {
        RuleFor(a => a.Name).ValidName();
        RuleFor(a => a.Description).ValidDescription();
        RuleFor(a => a.Members).Cascade(CascadeMode.Stop).ValidMembers();
        RuleForEach(a => a.Members).SetValidator(new ContactGroupMemberActionDtoValidator()).When(a => a.Members is not null);
    }
}
