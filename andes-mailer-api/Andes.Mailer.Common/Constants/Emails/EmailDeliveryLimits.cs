namespace Andes.Mailer.Common.Constants.Emails;

/// <summary>
/// Holds the column limits of an email delivery.
/// </summary>
public static class EmailDeliveryLimits
{
    /// <summary>
    /// The maximum length of the reason the last attempt failed.
    /// </summary>
    public const int LastErrorMaxLength = 1000;

    /// <summary>
    /// The maximum length of a Microsoft Graph message id.
    /// </summary>
    public const int GraphMessageIdMaxLength = 512;
}
