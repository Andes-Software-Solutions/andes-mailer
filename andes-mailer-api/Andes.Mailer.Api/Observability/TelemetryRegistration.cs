using Andes.Mailer.Api.Health;
using Andes.Mailer.Api.Options;
using Andes.Mailer.Common.Constants;
using Andes.Mailer.Common.Validation;

using FluentValidation;

using Microsoft.Extensions.Options;

using Microsoft.OpenTelemetry;

using OpenTelemetry;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Resources;

namespace Andes.Mailer.Api.Observability;

internal static class TelemetryRegistration
{
    #region Private methods

    private static string? ResolveConnectionString(IConfiguration configuration, ApplicationInsightsOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return options.ConnectionString;
        }

        var fromEnvironment = configuration[ApplicationInsightsOptions.ConnectionStringVariable];
        if (string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return null;
        }

        // The variable sits outside the bound section, so ValidateOnStart never sees it; the same rules run here instead.
        var result = new ApplicationInsightsOptionsValidator().Validate(new ApplicationInsightsOptions { ConnectionString = fromEnvironment });
        return !result.IsValid
            ? throw new OptionsValidationException(
                ApplicationInsightsOptions.ConnectionStringVariable,
                typeof(ApplicationInsightsOptions),
                [.. result.Errors.Select(error => $"{ApplicationInsightsOptions.ConnectionStringVariable}: {error.ErrorMessage}")])
            : fromEnvironment;
    }

    #endregion

    #region Public static methods

    public static IServiceCollection AddAndesMailerTelemetry(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidator<ApplicationInsightsOptions>, ApplicationInsightsOptionsValidator>();
        services.AddOptions<ApplicationInsightsOptions>()
            .Bind(configuration.GetSection(ApplicationInsightsOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        // Read now, not through IOptions: the distro wires its exporters while services are registered.
        var options = configuration.GetSection(ApplicationInsightsOptions.SectionName).Get<ApplicationInsightsOptions>() ?? new();
        var connectionString = ResolveConnectionString(configuration, options);
        var export = connectionString is not null;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(TelemetryNames.ServiceName))
            .UseMicrosoftOpenTelemetry(distro =>
            {
                // Explicit, so a stray OTLP or Agent 365 setting never turns another exporter on. Without a connection string the API
                // still traces, which keeps trace ids in logs and Problem Details, but sends nothing.
                distro.Exporters = export ? ExportTarget.AzureMonitor : ExportTarget.None;
                if (export)
                {
                    distro.AzureMonitor.ConnectionString = connectionString;
                    distro.AzureMonitor.TracesPerSecond = options.TracesPerSecond;

                    // Every request keeps its log line even when sampling drops its trace; the line carries the trace id and the caller.
                    distro.AzureMonitor.EnableTraceBasedLogsSampler = false;
                }
            })
            .WithTracing(tracing => tracing.AddSource(TelemetryNames.ActivitySourceName))
            .WithLogging(configureBuilder: null, configureOptions: logging => logging.IncludeScopes = true);

        // Probes poll every few seconds; tracing them would bury real requests and spend the sampling budget.
        services.AddOptions<AspNetCoreTraceInstrumentationOptions>()
            .Configure(instrumentation => instrumentation.Filter = context =>
                !context.Request.Path.StartsWithSegments(HealthRegistration.BasePath, StringComparison.OrdinalIgnoreCase));

        return services;
    }

    #endregion
}
