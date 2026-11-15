using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Service.Emails.Attachments;

/// <summary>
/// Identifies an accepted document type from a file's extension and confirms it against the file's leading bytes.
/// </summary>
public static class DocumentTypeDetector
{
    /// <summary>
    /// The number of leading bytes <see cref="Detect"/> needs to recognise every accepted type.
    /// </summary>
    public const int HeaderLength = 12;

    private static readonly Dictionary<string, DocumentTypes> _byExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = DocumentTypes.Pdf,
        [".docx"] = DocumentTypes.Docx,
        [".pptx"] = DocumentTypes.Pptx,
        [".png"] = DocumentTypes.Png,
        [".jpg"] = DocumentTypes.Jpeg,
        [".jpeg"] = DocumentTypes.Jpeg,
        [".gif"] = DocumentTypes.Gif,
        [".webp"] = DocumentTypes.Webp,
    };

    #region Public static methods

    /// <summary>
    /// Gets the accepted document type a file name's extension names.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The document type, or <see langword="null"/> if the extension is not accepted.</returns>
    public static DocumentTypes? FromFileName(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);

        return _byExtension.TryGetValue(Path.GetExtension(fileName), out var type) ? type : null;
    }

    /// <summary>
    /// Determines whether a file's leading bytes match a document type, so a renamed file of another kind is refused.
    /// </summary>
    /// <param name="type">The document type the extension names.</param>
    /// <param name="header">Up to <see cref="HeaderLength"/> leading bytes of the file.</param>
    /// <returns><see langword="true"/> if the bytes carry the type's signature; otherwise, <see langword="false"/>.</returns>
    public static bool Detect(DocumentTypes type, ReadOnlySpan<byte> header) => type switch
    {
        DocumentTypes.Pdf => header.StartsWith("%PDF-"u8),

        // OOXML is a ZIP package, so the signature cannot tell a .docx from a .pptx; the extension does.
        DocumentTypes.Docx or DocumentTypes.Pptx => header.StartsWith("PK\u0003\u0004"u8),
        DocumentTypes.Png => header.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
        DocumentTypes.Jpeg => header.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]),
        DocumentTypes.Gif => header.StartsWith("GIF87a"u8) || header.StartsWith("GIF89a"u8),
        DocumentTypes.Webp => header.Length >= HeaderLength && header.StartsWith("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
        _ => false,
    };

    /// <summary>
    /// Gets the media type a document type is served with.
    /// </summary>
    /// <param name="type">The document type.</param>
    /// <returns>The media type.</returns>
    public static string ContentTypeOf(DocumentTypes type) => type switch
    {
        DocumentTypes.Pdf => "application/pdf",
        DocumentTypes.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        DocumentTypes.Pptx => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        DocumentTypes.Png => "image/png",
        DocumentTypes.Jpeg => "image/jpeg",
        DocumentTypes.Gif => "image/gif",
        DocumentTypes.Webp => "image/webp",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "The document type has no media type."),
    };

    #endregion
}
