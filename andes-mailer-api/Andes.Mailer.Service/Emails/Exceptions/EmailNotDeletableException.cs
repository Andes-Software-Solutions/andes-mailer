using Andes.Mailer.Service.Exceptions;

namespace Andes.Mailer.Service.Emails.Exceptions;

/// <summary>
/// The exception thrown when an email that is scheduled or sending is deleted.
/// </summary>
public sealed class EmailNotDeletableException() : ConflictException("A scheduled or sending email cannot be deleted. Return it to a draft first, or wait until it finishes.");
