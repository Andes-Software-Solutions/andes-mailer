using Andes.Mailer.Common.Constants.ContactGroups;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.ContactGroups;

// A group is created and replaced with the same content, so the create and update validators share these rules.
internal static class ContactGroupRules
{
    #region Private methods

    // Trimmed and lowercased exactly as the service stores addresses, so the store's unique index never sees a duplicate.
    private static bool HaveDistinctAddresses(IReadOnlyList<ContactGroupMemberActionDto> members) =>
        members
            .Where(m => m?.Address is not null)
            .Select(m => m.Address.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Count() == members.Count(m => m?.Address is not null);

    #endregion

    #region Public static methods

    public static IRuleBuilderOptions<T, string> ValidName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(ContactGroupLimits.NameMaxLength);

    public static IRuleBuilderOptions<T, string?> ValidDescription<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(ContactGroupLimits.DescriptionMaxLength);

    public static IRuleBuilderOptions<T, IReadOnlyList<ContactGroupMemberActionDto>> ValidMembers<T>(this IRuleBuilder<T, IReadOnlyList<ContactGroupMemberActionDto>> rule) =>
        rule.NotNull()
            .Must(m => m.Count is > 0 and <= ContactGroupLimits.MembersMaxCount)
            .WithMessage($"'Members' must hold between 1 and {ContactGroupLimits.MembersMaxCount} addresses.")
            .Must(m => m.All(x => x is not null))
            .WithMessage("'Members' must not hold empty entries.")
            .Must(HaveDistinctAddresses)
            .WithMessage("'Members' must not repeat an address.");

    #endregion
}
