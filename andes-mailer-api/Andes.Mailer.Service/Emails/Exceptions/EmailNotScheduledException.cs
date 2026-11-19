using Andes.Mailer.Service.Exceptions;

namespace Andes.Mailer.Service.Emails.Exceptions;

/// <summary>
/// The exception thrown when an email that is not scheduled is asked to return to a draft.
/// </summary>
public sealed class EmailNotScheduledException() : ConflictException("Only a scheduled email can return to a draft.");
