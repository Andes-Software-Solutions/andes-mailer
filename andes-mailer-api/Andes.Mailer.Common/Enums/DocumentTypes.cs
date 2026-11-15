using System.ComponentModel;

namespace Andes.Mailer.Common.Enums;

/// <summary>
/// Names the kinds of document an email can carry as an attachment. Values are stored as keys of <c>[Core.Ref].[DocumentType]</c> and are never renumbered.
/// </summary>
public enum DocumentTypes
{
    /// <summary>
    /// A PDF document.
    /// </summary>
    [Description("PDF document (.pdf)")]
    Pdf = 1,

    /// <summary>
    /// A Word document in the Office Open XML format.
    /// </summary>
    [Description("Word document (.docx)")]
    Docx = 2,

    /// <summary>
    /// A PowerPoint presentation in the Office Open XML format.
    /// </summary>
    [Description("PowerPoint presentation (.pptx)")]
    Pptx = 3,

    /// <summary>
    /// A PNG image.
    /// </summary>
    [Description("PNG image (.png)")]
    Png = 4,

    /// <summary>
    /// A JPEG image.
    /// </summary>
    [Description("JPEG image (.jpg, .jpeg)")]
    Jpeg = 5,

    /// <summary>
    /// A GIF image.
    /// </summary>
    [Description("GIF image (.gif)")]
    Gif = 6,

    /// <summary>
    /// A WebP image.
    /// </summary>
    [Description("WebP image (.webp)")]
    Webp = 7,
}
