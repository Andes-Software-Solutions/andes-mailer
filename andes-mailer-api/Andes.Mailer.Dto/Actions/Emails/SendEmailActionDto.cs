using Andes.Mailer.Common.Constants.Emails;

using FluentValidation;

namespace Andes.Mailer.Dto.Actions.Emails;

/// <summary>
/// Sends a draft email now or schedules it for later.
/// </summary>
/// <param name="Version">The row version the caller last read, as base64; the send fails if the email changed since.</param>
/// <param name="ScheduledFor">When to send the email, or <see langword="null"/> to send it now.</param>
public sealed record SendEmailActionDto(string Version, DateTimeOffset? ScheduledFor);

/// <summary>
/// Validates a <see cref="SendEmailActionDto"/>.
/// </summary>
public sealed class SendEmailActionDtoValidator : AbstractValidator<SendEmailActionDto>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SendEmailActionDtoValidator"/> class.
    /// </summary>
    /// <param name="time">The clock a scheduled time is checked against.</param>
    public SendEmailActionDtoValidator(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);

        RuleFor(a => a.Version).ValidVersion();
        RuleFor(a => a.ScheduledFor)
            .Must(s => s > time.GetUtcNow())
            .WithMessage("'Scheduled For' must be in the future.")
            .Must(s => s <= time.GetUtcNow().AddDays(EmailSendLimits.MaxScheduleAheadDays))
            .WithMessage($"'Scheduled For' must be at most {EmailSendLimits.MaxScheduleAheadDays} days ahead.")
            .When(a => a.ScheduledFor is not null);
    }
}

