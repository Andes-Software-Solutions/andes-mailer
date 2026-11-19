using Andes.Mailer.Service.Exceptions;

namespace Andes.Mailer.Service.Emails.Exceptions;

/// <summary>
/// The exception thrown when a draft cannot be sent as it stands, such as one with no recipients or a failed attachment.
/// </summary>
/// <param name="message">Why the draft cannot be sent; it reaches the client as the problem detail.</param>
public sealed class EmailNotSendableException(string message) : ConflictException(message);
