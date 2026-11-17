using Andes.Mailer.Service.Exceptions;

namespace Andes.Mailer.Service.Emails.Exceptions;

/// <summary>
/// The exception thrown when an email is made from a template whose attachments are still uploading.
/// </summary>
public sealed class TemplateNotReadyException() : ConflictException("The template's attachments are still uploading. Try again once every attachment is Uploaded or Failed.");
