using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Base;
using Andes.Mailer.Entity.Emails.Interfaces;

namespace Andes.Mailer.Entity.Emails.Templates;

/// <summary>
/// Represents a document attached to a template and held in blob storage.
/// </summary>
/// <remarks>
/// An email made from the template gets its own copy of the document, so removing one never touches the other.
/// </remarks>
public sealed class EmailTemplateAttachment : BaseModifiedEntity<Guid>, IStagedAttachment
{
    /// <summary>
    /// Gets or sets the id of the template.
    /// </summary>
    public Guid TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the template.
    /// </summary>
    public EmailTemplate? Template { get; set; }

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

    /// <summary>
    /// Gets or sets the SHA-256 of the document, which tells an upload whether the same document is already attached.
    /// </summary>
    /// <value>The <see cref="Common.Constants.Emails.EmailAttachmentLimits.ContentHashLength"/>-byte hash, or <see langword="null"/> on rows saved before hashes were recorded.</value>
    public byte[]? ContentHash { get; set; }
}
