namespace Andes.Mailer.Service.Emails.Attachments.Models;

/// <summary>
/// Carries a stored attachment out to the caller that downloads it.
/// </summary>
/// <param name="Content">The document; the caller disposes it.</param>
/// <param name="ContentType">The media type the document is served with.</param>
/// <param name="FileName">The file name the uploader sent.</param>
public sealed record AttachmentContent(Stream Content, string ContentType, string FileName);
