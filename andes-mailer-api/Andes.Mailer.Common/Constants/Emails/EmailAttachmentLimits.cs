namespace Andes.Mailer.Common.Constants.Emails;

/// <summary>
/// Holds the column limits of an email attachment.
/// </summary>
public static class EmailAttachmentLimits
{
    /// <summary>
    /// The maximum length of a file name.
    /// </summary>
    public const int FileNameMaxLength = 255;

    /// <summary>
    /// The maximum length of a media type.
    /// </summary>
    public const int ContentTypeMaxLength = 127;

    /// <summary>
    /// The maximum length of a blob name.
    /// </summary>
    public const int BlobNameMaxLength = 200;

    /// <summary>
    /// The maximum length of a failure reason.
    /// </summary>
    public const int FailureReasonMaxLength = 256;

    /// <summary>
    /// The length of a document's SHA-256 hash, in bytes.
    /// </summary>
    public const int ContentHashLength = 32;
}
