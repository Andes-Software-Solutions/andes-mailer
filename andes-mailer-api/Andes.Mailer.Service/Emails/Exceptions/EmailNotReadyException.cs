using Andes.Mailer.Service.Exceptions;

namespace Andes.Mailer.Service.Emails.Exceptions;

/// <summary>
/// The exception thrown when an email is duplicated or sent while its attachments are still uploading.
/// </summary>
public sealed class EmailNotReadyException() : ConflictException("The email's attachments are still uploading. Try again once every attachment is Uploaded or Failed.");
