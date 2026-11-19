namespace Andes.Mailer.Service.Emails.Sending.Models;

/// <summary>
/// Describes a stored document to attach to an outgoing message.
/// </summary>
/// <param name="FileName">The file name the recipient sees.</param>
/// <param name="ContentType">The media type of the document.</param>
/// <param name="SizeBytes">The size of the document in bytes.</param>
/// <param name="BlobName">The name of the blob that holds the document.</param>
public sealed record OutgoingAttachment(string FileName, string ContentType, long SizeBytes, string BlobName);
