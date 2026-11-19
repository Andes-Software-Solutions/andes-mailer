using Cronos;

using FluentValidation;

namespace Andes.Mailer.Service.Options;

/// <summary>
/// Holds the settings of the background dispatcher that sends emails to their recipients.
/// </summary>
public sealed class EmailSendingOptions
{
    /// <summary>
    /// The configuration section these options bind to.
    /// </summary>
    public const string SectionName = "EmailSending";

    /// <summary>
    /// Gets a value indicating whether this instance runs the dispatcher. The default is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Several instances can run it against one database: each claims a delivery before sending it, so no delivery is sent twice.
    /// </remarks>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Gets the CRON expression, in UTC, of when the dispatcher looks for due deliveries. The default is every minute.
    /// </summary>
    /// <remarks>
    /// Five fields, or six with seconds first. Sending an email now wakes the dispatcher at once, whatever the schedule.
    /// </remarks>
    public string Schedule { get; init; } = "* * * * *";

    /// <summary>
    /// Gets the number of times a delivery is tried before it is marked failed. The default is 3.
    /// </summary>
    /// <remarks>
    /// Throttling by the mail server does not count as an attempt; an attempt the process dies in the middle of does.
    /// </remarks>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Gets the largest number of deliveries one run looks at. The default is 100.
    /// </summary>
    public int BatchSize { get; init; } = 100;

    /// <summary>
    /// Gets the number of mailboxes sent from at once. The default is 4.
    /// </summary>
    /// <remarks>
    /// Deliveries from one mailbox always go one after another.
    /// </remarks>
    public int MaxConcurrency { get; init; } = 4;

    /// <summary>
    /// Gets the largest number of messages one run sends from one mailbox. The default is 30.
    /// </summary>
    /// <remarks>
    /// Exchange Online accepts about 30 messages a minute from a mailbox, so with the default schedule this keeps under the limit.
    /// </remarks>
    public int MaxPerMailboxPerRun { get; init; } = 30;

    /// <summary>
    /// Gets how long a claim on a delivery lasts before another run may pick it up again. The default is 5 minutes.
    /// </summary>
    /// <remarks>
    /// Longer than the slowest send, attachments included; a claim that runs out counts as a failed attempt.
    /// </remarks>
    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets the wait before the first retry; each later retry waits twice as long. The default is 1 minute.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets the longest wait between retries. The default is 30 minutes.
    /// </summary>
    public TimeSpan RetryMaxDelay { get; init; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Validates <see cref="EmailSendingOptions"/>. Public because the Api composition root registers it.
/// </summary>
public sealed class EmailSendingOptionsValidator : AbstractValidator<EmailSendingOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSendingOptionsValidator"/> class.
    /// </summary>
    public EmailSendingOptionsValidator()
    {
        RuleFor(o => o.Schedule)
            .Must(BeACronExpression)
            .WithMessage("'Schedule' must be a CRON expression of five fields, or six with seconds first.");
        RuleFor(o => o.MaxAttempts).InclusiveBetween(1, 10);
        RuleFor(o => o.BatchSize).InclusiveBetween(1, 1_000);
        RuleFor(o => o.MaxConcurrency).InclusiveBetween(1, 64);
        RuleFor(o => o.MaxPerMailboxPerRun).InclusiveBetween(1, 1_000);
        RuleFor(o => o.LeaseDuration).InclusiveBetween(TimeSpan.FromSeconds(30), TimeSpan.FromHours(1));
        RuleFor(o => o.RetryBaseDelay).InclusiveBetween(TimeSpan.FromSeconds(1), TimeSpan.FromHours(1));
        RuleFor(o => o.RetryMaxDelay).GreaterThanOrEqualTo(o => o.RetryBaseDelay).LessThanOrEqualTo(TimeSpan.FromDays(1));
    }

    #region Private methods

    private static bool BeACronExpression(string schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule))
        {
            return false;
        }

        try
        {
            ParseSchedule(schedule);

            return true;
        }
        catch (CronFormatException)
        {
            return false;
        }
    }

    #endregion

    #region Public static methods

    /// <summary>
    /// Parses a schedule, with or without a seconds field.
    /// </summary>
    /// <param name="schedule">The CRON expression.</param>
    /// <returns>The parsed expression.</returns>
    /// <exception cref="CronFormatException">The expression is not valid CRON.</exception>
    public static CronExpression ParseSchedule(string schedule)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schedule);

        var format = schedule.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 6 ? CronFormat.IncludeSeconds : CronFormat.Standard;

        return CronExpression.Parse(schedule, format);
    }

    #endregion
}
