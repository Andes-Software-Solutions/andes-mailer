using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Dto.Emails;

/// <summary>
/// Describes a document attached to an email or a template, and the progress of its upload to storage.
/// </summary>
/// <param name="Id">The attachment's id.</param>
/// <param name="FileName">The file name the uploader sent.</param>
/// <param name="DocumentType">One of the enumeration values that specifies the kind of document.</param>
/// <param name="ContentType">The media type the document is served with.</param>
/// <param name="SizeBytes">The size of the document in bytes.</param>
/// <param name="Status">One of the enumeration values that specifies the stage of the upload; poll until it is <see cref="AttachmentStatuses.Uploaded"/> or <see cref="AttachmentStatuses.Failed"/>.</param>
/// <param name="FailureReason">Why the upload failed, or <see langword="null"/> unless <paramref name="Status"/> is <see cref="AttachmentStatuses.Failed"/>.</param>
/// <param name="DateCreated">The moment the document was received.</param>
public sealed record EmailAttachmentDto(
    Guid Id,
    string FileName,
    DocumentTypes DocumentType,
    string ContentType,
    long SizeBytes,
    AttachmentStatuses Status,
    string? FailureReason,
    DateTimeOffset DateCreated);
