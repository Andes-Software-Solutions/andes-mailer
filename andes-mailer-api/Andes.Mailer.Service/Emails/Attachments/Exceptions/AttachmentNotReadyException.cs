using Andes.Mailer.Service.Exceptions;

namespace Andes.Mailer.Service.Emails.Attachments.Exceptions;

/// <summary>
/// The exception thrown when an attachment is downloaded before its upload to storage has finished.
/// </summary>
public sealed class AttachmentNotReadyException() : ConflictException("The attachment is not stored yet. Wait until its status is Uploaded.");
