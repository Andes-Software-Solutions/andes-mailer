using System.ComponentModel;

namespace Andes.Mailer.Common.Enums;

/// <summary>
/// Names the stages of delivering an email to one recipient. Values are stored as keys of <c>[Core.Ref].[DeliveryStatus]</c> and are
/// never renumbered.
/// </summary>
public enum DeliveryStatuses
{
    /// <summary>
    /// Waiting for its next attempt.
    /// </summary>
    [Description("Waiting for its next attempt")]
    Pending = 1,

    /// <summary>
    /// Claimed by the dispatcher and being sent.
    /// </summary>
    [Description("Being sent")]
    Sending = 2,

    /// <summary>
    /// Accepted by the mail server; it is never sent again.
    /// </summary>
    [Description("Accepted by the mail server")]
    Sent = 3,

    /// <summary>
    /// Every attempt failed, or the failure cannot be retried.
    /// </summary>
    [Description("Every attempt failed")]
    Failed = 4,
}
