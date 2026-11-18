using Andes.Mailer.Common.Constants.Emails;

namespace Andes.Mailer.Common.Constants.ContactGroups;

/// <summary>
/// Holds the column and size limits of a contact group, shared by the store mapping and the request validators.
/// </summary>
public static class ContactGroupLimits
{
    /// <summary>
    /// The maximum length of a name.
    /// </summary>
    public const int NameMaxLength = 100;

    /// <summary>
    /// The maximum length of a description.
    /// </summary>
    public const int DescriptionMaxLength = 500;

    /// <summary>
    /// The maximum number of members.
    /// </summary>
    /// <remarks>
    /// The same as an email's recipient cap, so a whole group always fits in one email.
    /// </remarks>
    public const int MembersMaxCount = EmailLimits.RecipientsMaxCount;

    /// <summary>
    /// The maximum length of a member's address; the same as an email recipient's, since members become recipients.
    /// </summary>
    public const int AddressMaxLength = EmailRecipientLimits.AddressMaxLength;

    /// <summary>
    /// The maximum length of a member's display name; the same as an email recipient's, since members become recipients.
    /// </summary>
    public const int DisplayNameMaxLength = EmailRecipientLimits.DisplayNameMaxLength;

    /// <summary>
    /// The maximum length of the text a group list is searched by.
    /// </summary>
    public const int SearchMaxLength = 100;
}
