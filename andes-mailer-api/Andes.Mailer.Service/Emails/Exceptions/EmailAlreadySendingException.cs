using Andes.Mailer.Service.Exceptions;

namespace Andes.Mailer.Service.Emails.Exceptions;

/// <summary>
/// The exception thrown when a scheduled email has started sending and can no longer return to a draft.
/// </summary>
public sealed class EmailAlreadySendingException() : ConflictException("The email has started sending and can no longer return to a draft.");
