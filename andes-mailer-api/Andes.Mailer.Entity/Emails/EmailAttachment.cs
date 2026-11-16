using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Base;

namespace Andes.Mailer.Entity.Emails;

/// <summary>
/// Represents a document attached to an email and held in blob storage.
/// </summary>
public sealed class EmailAttachment : BaseModifiedEntity<Guid>
{
    /// <summary>
    /// Gets or sets the id of the email.
    /// </summary>
    public Guid EmailId { get; set; }

    /// <summary>
    /// Gets or sets the email.
    /// </summary>
    public Email? Email { get; set; }

    /// <summary>
    /// Gets or sets the file name the uploader sent, without any directory.
    /// </summary>
    public required string FileName { get; set; }

    /// <summary>
    /// Gets or sets the kind of document.
    /// </summary>
    public DocumentTypes DocumentTypeId { get; set; }

    /// <summary>
    /// Gets or sets the reference row of the document type.
    /// </summary>
    public DocumentType? DocumentType { get; set; }

    /// <summary>
    /// Gets or sets the media type the document is served with.
    /// </summary>
    /// <value>The media type of <see cref="DocumentTypeId"/>, never the one the client declared.</value>
    public required string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the size of the document in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the name of the blob that holds the document.
    /// </summary>
    public required string BlobName { get; set; }

    /// <summary>
    /// Gets or sets the stage of the upload to storage.
    /// </summary>
    public AttachmentStatuses StatusId { get; set; }

    /// <summary>
    /// Gets or sets the reference row of the status.
    /// </summary>
    public AttachmentStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets why the upload failed.
    /// </summary>
    /// <value>A message safe to show the team, or <see langword="null"/> unless <see cref="StatusId"/> is <see cref="AttachmentStatuses.Failed"/>.</value>
    public string? FailureReason { get; set; }
}
