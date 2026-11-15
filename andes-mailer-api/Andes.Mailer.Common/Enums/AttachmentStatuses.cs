using System.ComponentModel;

namespace Andes.Mailer.Common.Enums;

/// <summary>
/// Names the stages of an attachment's upload to storage. Values are stored as keys of <c>[Core.Ref].[AttachmentStatus]</c> and are never renumbered.
/// </summary>
public enum AttachmentStatuses
{
    /// <summary>
    /// Received and waiting for the background upload.
    /// </summary>
    [Description("Received and waiting to be stored")]
    Pending = 1,

    /// <summary>
    /// Being written to storage.
    /// </summary>
    [Description("Being written to storage")]
    Uploading = 2,

    /// <summary>
    /// Stored and ready to download.
    /// </summary>
    [Description("Stored and ready to download")]
    Uploaded = 3,

    /// <summary>
    /// Could not be stored; upload the file again.
    /// </summary>
    [Description("Could not be stored")]
    Failed = 4,
}
