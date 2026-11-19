using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Service.Emails.Sending.Exceptions;

/// <summary>
/// The exception thrown when the team's mailbox could not create or send a message.
/// </summary>
/// <param name="kind">One of the enumeration values that specifies whether the delivery is tried again.</param>
/// <param name="message">A message safe to show the team; it never holds an address or any content.</param>
/// <param name="retryAfter">How long the mail server asked the caller to wait, or <see langword="null"/> if it did not say.</param>
/// <param name="innerException">The fault the mail server reported.</param>
public sealed class MailboxException(MailboxFailureKinds kind, string message, TimeSpan? retryAfter = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>
    /// Gets how the call failed.
    /// </summary>
    public MailboxFailureKinds Kind { get; } = kind;

    /// <summary>
    /// Gets how long the mail server asked the caller to wait.
    /// </summary>
    /// <value>The wait, or <see langword="null"/> if the server did not say.</value>
    public TimeSpan? RetryAfter { get; } = retryAfter;
}
