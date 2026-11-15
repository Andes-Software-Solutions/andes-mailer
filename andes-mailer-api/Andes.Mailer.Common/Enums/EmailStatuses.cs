using System.ComponentModel;

namespace Andes.Mailer.Common.Enums;

/// <summary>
/// Names the stages of an email's life. Values are stored as keys of <c>[Core.Ref].[EmailStatus]</c> and are never renumbered.
/// </summary>
public enum EmailStatuses
{
    /// <summary>
    /// Being composed; the sender can still change it.
    /// </summary>
    [Description("Being composed")]
    Draft = 1,

    /// <summary>
    /// Sent; it can no longer change.
    /// </summary>
    [Description("Sent; reserved until sending is built")]
    Sent = 2,
}
