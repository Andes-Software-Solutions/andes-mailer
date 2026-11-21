using Andes.Mailer.Repository.Blob;
using Andes.Mailer.Repository.Sql;

using Microsoft.AspNetCore.Diagnostics.HealthChecks;

namespace Andes.Mailer.Api.Health;

internal static class HealthRegistration
{
    public const string BasePath = "/health";

    private const string LivePath = BasePath + "/live";
    private const string ReadyPath = BasePath + "/ready";
    private const string ReadyTag = "ready";
    private const string SqlCheckName = "sql";
    private const string BlobCheckName = "blob";

    // Connection retries can run far longer than a readiness probe waits, so each check gives up first.
    private static readonly TimeSpan _sqlCheckTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _blobCheckTimeout = TimeSpan.FromSeconds(5);

    #region Public static methods

    public static IServiceCollection AddAndesMailerHealth(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddAndesMailerSqlHealthCheck(SqlCheckName, [ReadyTag], _sqlCheckTimeout)
            .AddAndesMailerBlobHealthCheck(BlobCheckName, [ReadyTag], _blobCheckTimeout);

        return services;
    }

    public static IEndpointRouteBuilder MapAndesMailerHealth(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks(LivePath, new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
        app.MapHealthChecks(ReadyPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) }).AllowAnonymous();

        return app;
    }

    #endregion
}
