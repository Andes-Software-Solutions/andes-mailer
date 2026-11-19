namespace Andes.Mailer.Common.Constants.Emails;

/// <summary>
/// Holds the limits on sending and scheduling an email.
/// </summary>
public static class EmailSendLimits
{
    /// <summary>
    /// The furthest ahead, in days, an email can be scheduled.
    /// </summary>
    public const int MaxScheduleAheadDays = 365;
}
