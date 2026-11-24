namespace Andes.Mailer.Common.Constants.Teams;

/// <summary>
/// Holds the column limits of a team, shared by the store mapping and the request validators.
/// </summary>
public static class TeamLimits
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
    /// The maximum length of the service account's email address.
    /// </summary>
    public const int ServiceAccountEmailMaxLength = 256;

    /// <summary>
    /// The maximum length of the default signature's HTML as the client sends it, in characters.
    /// </summary>
    /// <remarks>
    /// Checked on the request only: sanitizing can lengthen the HTML by encoding characters, so the column itself is unbounded, like an email body.
    /// </remarks>
    public const int DefaultSignatureMaxLength = 4000;

    /// <summary>
    /// The smallest recipient count that can trigger a confirmation before sending.
    /// </summary>
    public const int ConfirmRecipientsAboveMin = 1;

    /// <summary>
    /// The largest recipient count that can trigger a confirmation before sending: the recipient cap of an email.
    /// </summary>
    public const int ConfirmRecipientsAboveMax = Emails.EmailLimits.RecipientsMaxCount;
}
