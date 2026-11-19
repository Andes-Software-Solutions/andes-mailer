using System.ComponentModel;

namespace Andes.Mailer.Common.Enums;

/// <summary>
/// Names the stages of an email's life. Values are stored as keys of <c>[Core.Ref].[EmailStatus]</c> and are never renumbered.
/// </summary>
public enum EmailStatuses
{
    /// <summary>
    /// Being composed; the team can still change it.
    /// </summary>
    [Description("Being composed")]
    Draft = 1,

    /// <summary>
    /// Sent to every recipient; it can no longer change.
    /// </summary>
    [Description("Sent to every recipient")]
    Sent = 2,

    /// <summary>
    /// Waiting for its scheduled time; it can return to a draft until a recipient is picked up.
    /// </summary>
    [Description("Waiting for its scheduled time")]
    Scheduled = 3,

    /// <summary>
    /// Being delivered to its recipients.
    /// </summary>
    [Description("Being delivered to its recipients")]
    Sending = 4,

    /// <summary>
    /// Finished; some recipients received it and the rest failed.
    /// </summary>
    [Description("Sent to some recipients; the rest failed")]
    PartiallySent = 5,

    /// <summary>
    /// Finished; no recipient received it.
    /// </summary>
    [Description("No recipient received it")]
    Failed = 6,
}
