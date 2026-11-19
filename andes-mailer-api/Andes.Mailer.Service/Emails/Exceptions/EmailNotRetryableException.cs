using Andes.Mailer.Service.Exceptions;

namespace Andes.Mailer.Service.Emails.Exceptions;

/// <summary>
/// The exception thrown when a retry targets an email that did not finish with failed recipients.
/// </summary>
public sealed class EmailNotRetryableException() : ConflictException("Only an email that finished with failed recipients can be retried.");
