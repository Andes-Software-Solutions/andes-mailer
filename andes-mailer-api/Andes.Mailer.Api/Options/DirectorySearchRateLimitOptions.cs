using FluentValidation;

namespace Andes.Mailer.Api.Options;

/// <summary>
/// Holds the per-caller token bucket that limits directory searches, so a client that skips its debounce cannot exhaust the Graph quota.
/// </summary>
public sealed class DirectorySearchRateLimitOptions
{
    /// <summary>
    /// The configuration section these options bind to.
    /// </summary>
    public const string SectionName = "RateLimiting:DirectorySearch";

    /// <summary>
    /// Gets the largest burst of searches a caller can make at once. The default is 20.
    /// </summary>
    public int TokenLimit { get; init; } = 20;

    /// <summary>
    /// Gets the number of searches given back to a caller each period. The default is 10.
    /// </summary>
    public int TokensPerPeriod { get; init; } = 10;

    /// <summary>
    /// Gets the time between refills. The default is 10 seconds.
    /// </summary>
    public TimeSpan ReplenishmentPeriod { get; init; } = TimeSpan.FromSeconds(10);
}

internal sealed class DirectorySearchRateLimitOptionsValidator : AbstractValidator<DirectorySearchRateLimitOptions>
{
    public DirectorySearchRateLimitOptionsValidator()
    {
        RuleFor(o => o.TokenLimit).GreaterThan(0);
        RuleFor(o => o.TokensPerPeriod).GreaterThan(0);
        RuleFor(o => o.ReplenishmentPeriod).InclusiveBetween(TimeSpan.FromSeconds(1), TimeSpan.FromHours(1));
    }
}
