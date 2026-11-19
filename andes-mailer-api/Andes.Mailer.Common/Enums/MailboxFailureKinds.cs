namespace Andes.Mailer.Common.Enums;

/// <summary>
/// Names how a call to the team's mailbox failed, which decides whether the delivery is tried again.
/// </summary>
public enum MailboxFailureKinds
{
    /// <summary>
    /// The mail server asked the caller to slow down; the attempt is not counted.
    /// </summary>
    Throttled = 1,

    /// <summary>
    /// A fault that may pass, such as a server error or a timeout; the delivery is tried again.
    /// </summary>
    Transient = 2,

    /// <summary>
    /// A fault that another attempt cannot fix, such as an invalid recipient or a mailbox the application cannot use.
    /// </summary>
    Permanent = 3,
}
