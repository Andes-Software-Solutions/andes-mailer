using Andes.Mailer.Repository.Sql;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Andes.Mailer.Api.Health;

internal static class HealthRegistration
{
    private const string LivePath = "/health/live";
    private const string ReadyPath = "/health/ready";
    private const string ReadyTag = "ready";
    private const string SqlCheckName = "sql";

    // Connection retries can run far longer than a readiness probe waits, so the check gives up first.
    private static readonly TimeSpan _sqlCheckTimeout = TimeSpan.FromSeconds(5);

    #region Public static methods

    public static IServiceCollection AddAndesMailerHealth(this IServiceCollection services)
    {
        services.AddHealthChecks().AddAndesMailerSqlHealthCheck(SqlCheckName, [ReadyTag], _sqlCheckTimeout);

        return services;
    }

    public static IEndpointRouteBuilder MapAndesMailerHealth(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks(ReadyPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) });

        return app;
    }

    #endregion
}
