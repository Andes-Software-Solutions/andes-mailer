using Andes.Mailer.Common.Constants.ContactGroups;
using Andes.Mailer.Dto.Actions.Emails;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.ContactGroups;

/// <summary>
/// Adds one address to a contact group.
/// </summary>
/// <param name="Address">The member's email address, inside or outside the directory.</param>
public sealed record ContactGroupMemberActionDto(string Address);

/// <summary>
/// Validates a <see cref="ContactGroupMemberActionDto"/>.
/// </summary>
public sealed class ContactGroupMemberActionDtoValidator : AbstractValidator<ContactGroupMemberActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContactGroupMemberActionDtoValidator"/> class.
    /// </summary>
    public ContactGroupMemberActionDtoValidator()
    {
        RuleFor(m => m.Address).PlainAddress(ContactGroupLimits.AddressMaxLength);
    }
}
