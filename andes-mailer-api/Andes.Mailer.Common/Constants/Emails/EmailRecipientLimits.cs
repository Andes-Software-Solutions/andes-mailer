namespace Andes.Mailer.Common.Constants.Emails;

/// <summary>
/// Holds the column limits of an email recipient, shared by the store mapping and the request validators.
/// </summary>
public static class EmailRecipientLimits
{
    /// <summary>
    /// The maximum length of an address.
    /// </summary>
    public const int AddressMaxLength = 256;

    /// <summary>
    /// The maximum length of a display name.
    /// </summary>
    public const int DisplayNameMaxLength = 256;
}
