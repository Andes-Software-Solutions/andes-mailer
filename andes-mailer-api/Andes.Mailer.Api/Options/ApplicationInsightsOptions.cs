using FluentValidation;

namespace Andes.Mailer.Api.Options;

/// <summary>
/// Holds where the API sends its traces, logs, and metrics. With no connection string here or in
/// <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c>, the API still traces its work but exports nothing.
/// </summary>
public sealed class ApplicationInsightsOptions
{
    /// <summary>
    /// The configuration section these options bind to.
    /// </summary>
    public const string SectionName = "ApplicationInsights";

    /// <summary>
    /// The environment variable Azure App Service sets when Application Insights is turned on for the app. It is read when
    /// <see cref="ConnectionString"/> is empty.
    /// </summary>
    public const string ConnectionStringVariable = "APPLICATIONINSIGHTS_CONNECTION_STRING";

    /// <summary>
    /// Gets the connection string of the Application Insights resource, as the resource's overview page shows it.
    /// </summary>
    public string? ConnectionString { get; init; }

    /// <summary>
    /// Gets the most traces a second the API sends. A trace beyond it is dropped whole, so a sent trace is never left incomplete; logs are
    /// always sent, so every request keeps its log line. The default is 5, the distro's own.
    /// </summary>
    public double TracesPerSecond { get; init; } = 5;
}

internal sealed class ApplicationInsightsOptionsValidator : AbstractValidator<ApplicationInsightsOptions>
{
    private const string InstrumentationKeyPart = "InstrumentationKey=";

    public ApplicationInsightsOptionsValidator()
    {
        RuleFor(o => o.ConnectionString)
            .Must(c => c!.Contains(InstrumentationKeyPart, StringComparison.OrdinalIgnoreCase))
            .When(o => !string.IsNullOrWhiteSpace(o.ConnectionString))
            .WithMessage($"'{nameof(ApplicationInsightsOptions.ConnectionString)}' must be the resource's connection string, which holds an {InstrumentationKeyPart} part.");
        RuleFor(o => o.TracesPerSecond).InclusiveBetween(0.01, 1000);
    }
}
