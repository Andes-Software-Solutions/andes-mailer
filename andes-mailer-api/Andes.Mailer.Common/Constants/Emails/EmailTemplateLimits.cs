namespace Andes.Mailer.Common.Constants.Emails;

/// <summary>
/// Holds the column limits of an email template, shared by the store mapping and the request validators.
/// </summary>
/// <remarks>
/// A template's subject, body, and recipients share the limits in <see cref="EmailLimits"/>, so any template fits the email made from it.
/// </remarks>
public static class EmailTemplateLimits
{
    /// <summary>
    /// The maximum length of a name.
    /// </summary>
    public const int NameMaxLength = 128;
}
