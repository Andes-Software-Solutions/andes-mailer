namespace Andes.Mailer.Common.Constants.Emails;

/// <summary>
/// Holds the column limits of an email, shared by the store mapping and the request validators.
/// </summary>
public static class EmailLimits
{
    /// <summary>
    /// The maximum length of a subject.
    /// </summary>
    public const int SubjectMaxLength = 255;

    /// <summary>
    /// The maximum length of an HTML body, in characters.
    /// </summary>
    public const int BodyHtmlMaxLength = 1_000_000;

    /// <summary>
    /// The maximum number of recipients across To, Cc, and Bcc.
    /// </summary>
    public const int RecipientsMaxCount = 500;
}
