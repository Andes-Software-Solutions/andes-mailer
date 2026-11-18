using System.Buffers.Text;

using Andes.Mailer.Common.Constants.Base;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.ContactGroups;

/// <summary>
/// Replaces a contact group's name, description, and members.
/// </summary>
/// <param name="Name">The new name, unique among the team's active groups.</param>
/// <param name="Description">The new description, or <see langword="null"/> for none.</param>
/// <param name="Members">The members' addresses, in the order they are listed; addresses left out are removed.</param>
/// <param name="Version">The row version the caller last read, as base64; the update fails if the group changed since.</param>
public sealed record UpdateContactGroupActionDto(string Name, string? Description, IReadOnlyList<ContactGroupMemberActionDto> Members, string Version);

/// <summary>
/// Validates an <see cref="UpdateContactGroupActionDto"/>.
/// </summary>
public sealed class UpdateContactGroupActionDtoValidator : AbstractValidator<UpdateContactGroupActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateContactGroupActionDtoValidator"/> class.
    /// </summary>
    public UpdateContactGroupActionDtoValidator()
    {
        RuleFor(a => a.Name).ValidName();
        RuleFor(a => a.Description).ValidDescription();
        RuleFor(a => a.Members).Cascade(CascadeMode.Stop).ValidMembers();
        RuleForEach(a => a.Members).SetValidator(new ContactGroupMemberActionDtoValidator()).When(a => a.Members is not null);

        // The decoded length too: well-formed base64 of the wrong size would otherwise reach the store and come back as a 409.
        RuleFor(a => a.Version)
            .NotEmpty()
            .Must(v => Base64.IsValid(v, out var length) && length == BaseModifiedEntityLimits.VersionLength)
            .WithMessage("'Version' must be the base64 row version returned by the API.");
    }
}
