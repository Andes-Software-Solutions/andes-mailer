using Andes.Mailer.Common.Constants.Emails;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Emails;

// Emails and templates accept the same content, so their create and update validators share these rules.
internal static class EmailContentRules
{
    #region Private methods

    // Trimmed and case-insensitive, the way the service stores addresses, so the store's unique index never sees a duplicate.
    private static bool HaveDistinctAddresses(IReadOnlyList<EmailRecipientActionDto> recipients) =>
        recipients
            .Where(r => r.Address is not null)
            .Select(r => r.Address.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count() == recipients.Count(r => r.Address is not null);

    #endregion

    #region Public static methods

    public static IRuleBuilderOptions<T, string> ValidTemplateName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(EmailTemplateLimits.NameMaxLength);

    public static IRuleBuilderOptions<T, string> ValidSubject<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotNull().MaximumLength(EmailLimits.SubjectMaxLength);

    public static IRuleBuilderOptions<T, string> ValidBodyHtml<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotNull().MaximumLength(EmailLimits.BodyHtmlMaxLength);

    public static IRuleBuilderOptions<T, IReadOnlyList<EmailRecipientActionDto>> ValidRecipients<T>(this IRuleBuilder<T, IReadOnlyList<EmailRecipientActionDto>> rule) =>
        rule.NotNull()
            .Must(r => r.Count <= EmailLimits.RecipientsMaxCount)
            .WithMessage($"'Recipients' must not hold more than {EmailLimits.RecipientsMaxCount} addresses.")
            .Must(r => r.All(x => x is not null))
            .WithMessage("'Recipients' must not hold empty entries.")
            .Must(HaveDistinctAddresses)
            .WithMessage("'Recipients' must not repeat an address, even under a different header.");

    #endregion
}
