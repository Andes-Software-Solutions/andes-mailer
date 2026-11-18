using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Entity.Emails.Interfaces;

/// <summary>
/// Describes a document that is staged on local disk and uploaded to blob storage in the background, whatever it is attached to.
/// </summary>
public interface IStagedAttachment
{
    /// <summary>
    /// Gets the primary key.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Gets the moment the attachment was received.
    /// </summary>
    DateTimeOffset DateCreated { get; }

    /// <summary>
    /// Gets the media type the document is served with.
    /// </summary>
    string ContentType { get; }

    /// <summary>
    /// Gets the name of the blob that holds the document.
    /// </summary>
    string BlobName { get; }

    /// <summary>
    /// Gets or sets the size of the document in bytes.
    /// </summary>
    long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the stage of the upload to storage.
    /// </summary>
    AttachmentStatuses StatusId { get; set; }

    /// <summary>
    /// Gets or sets why the upload failed.
    /// </summary>
    /// <value>A message safe to show the team, or <see langword="null"/> unless <see cref="StatusId"/> is <see cref="AttachmentStatuses.Failed"/>.</value>
    string? FailureReason { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 of the document, which tells an upload whether the same document is already attached.
    /// </summary>
    /// <value>The <see cref="Common.Constants.Emails.EmailAttachmentLimits.ContentHashLength"/>-byte hash, or <see langword="null"/> on rows saved before hashes were recorded.</value>
    byte[]? ContentHash { get; set; }
}
