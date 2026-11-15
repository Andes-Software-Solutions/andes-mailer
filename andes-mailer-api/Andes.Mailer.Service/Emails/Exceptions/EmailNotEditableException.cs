using Andes.Mailer.Service.Exceptions;

namespace Andes.Mailer.Service.Emails.Exceptions;

/// <summary>
/// The exception thrown when a change targets an email that is no longer a draft.
/// </summary>
public sealed class EmailNotEditableException() : ConflictException("Only a draft email can change.");
