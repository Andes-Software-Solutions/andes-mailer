using System.ComponentModel;

namespace Andes.Mailer.Common.Enums;

/// <summary>
/// Names the header an email recipient is addressed in. Values are stored as keys of <c>[Core.Ref].[RecipientType]</c> and are never renumbered.
/// </summary>
public enum RecipientTypes
{
    /// <summary>
    /// A primary recipient.
    /// </summary>
    [Description("Primary recipient")]
    To = 1,

    /// <summary>
    /// A copied recipient, visible to every other recipient.
    /// </summary>
    [Description("Copied recipient")]
    Cc = 2,

    /// <summary>
    /// A blind-copied recipient, hidden from every other recipient.
    /// </summary>
    [Description("Blind-copied recipient")]
    Bcc = 3,
}
