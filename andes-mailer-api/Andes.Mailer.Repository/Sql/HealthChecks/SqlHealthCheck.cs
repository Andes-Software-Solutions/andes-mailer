using Andes.Mailer.Repository.Sql.DbContexts;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Andes.Mailer.Repository.Sql.HealthChecks;

internal sealed class SqlHealthCheck(AndesMailerDbContext ctx) : IHealthCheck
{
    private readonly AndesMailerDbContext _ctx = ctx;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connected = await _ctx.Database.CanConnectAsync(cancellationToken);

        return connected ? HealthCheckResult.Healthy() : new HealthCheckResult(context.Registration.FailureStatus);
    }
}
