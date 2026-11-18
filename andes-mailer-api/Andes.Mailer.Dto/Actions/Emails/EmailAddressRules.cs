using System.Net.Mail;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Emails;

// Email recipients and contact group members accept the same addresses, since a group's members become an email's recipients.
internal static class EmailAddressRules
{
    #region Private methods

    private static bool IsPlainAddress(string address)
    {
        var trimmed = address.Trim();

        return MailAddress.TryCreate(trimmed, out var parsed)
            && string.Equals(parsed.Address, trimmed, StringComparison.Ordinal)
            && !trimmed.Any(char.IsWhiteSpace);
    }

    #endregion

    #region Public static methods

    // FluentValidation's EmailAddress() only checks for an inner '@'; MailAddress also refuses whitespace, control characters, and display names,
    // which would otherwise reach the directory filter and, later, the message headers.
    public static IRuleBuilderOptions<T, string> PlainAddress<T>(this IRuleBuilderInitial<T, string> rule, int maxLength) =>
        rule.Cascade(CascadeMode.Stop)
            .NotEmpty()
            .MaximumLength(maxLength)
            .Must(IsPlainAddress)
            .WithMessage("'{PropertyName}' must be a single email address with no name or spaces.");

    #endregion
}
