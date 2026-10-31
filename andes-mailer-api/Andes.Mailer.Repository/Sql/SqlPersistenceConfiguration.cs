using Andes.Mailer.Common.Validation;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Repository.Sql.HealthChecks;
using Andes.Mailer.Repository.Sql.Interceptors;
using Andes.Mailer.Repository.Sql.Options;

using FluentValidation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Andes.Mailer.Repository.Sql;

/// <summary>
/// Registers the SQL Server store.
/// </summary>
public static class SqlPersistenceConfiguration
{
    // UseSqlServer otherwise assumes level 150 (SQL Server 2019), which leaves the json type and 2025 SQL generation off.
    private const int SqlServer2025CompatibilityLevel = 170;

    #region Public static methods

    /// <summary>
    /// Registers the DbContext and its options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration holding the <see cref="SqlDbOptions"/> section.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddAndesMailerSqlPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidator<SqlDbOptions>, SqlDbOptionsValidator>();
        services.AddOptions<SqlDbOptions>()
            .Bind(configuration.GetSection(SqlDbOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<AndesMailerDbContext>((sp, options) => options.UseAndesMailerSqlServer(
            sp.GetRequiredService<IOptions<SqlDbOptions>>().Value.ConnectionString,
            sp.GetRequiredService<TimeProvider>(),
            retryOnFailure: true));

        return services;
    }

    /// <summary>
    /// Configures the provider and interceptors of the SQL Server store, so production and test fixtures build the same options.
    /// </summary>
    /// <param name="options">The options builder to configure.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="time">The clock the interceptors stamp with.</param>
    /// <param name="retryOnFailure"><see langword="true"/> to retry transient failures; otherwise, <see langword="false"/>. A test that wraps its work in a transaction needs <see langword="false"/>, because a retrying strategy rejects it.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static DbContextOptionsBuilder UseAndesMailerSqlServer(this DbContextOptionsBuilder options, string connectionString, TimeProvider time, bool retryOnFailure)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);

        return options
            .UseSqlServer(connectionString, sql =>
            {
                sql.UseCompatibilityLevel(SqlServer2025CompatibilityLevel);
                if (retryOnFailure)
                {
                    sql.EnableRetryOnFailure();
                }
            })
            .AddInterceptors(new DeactivationInterceptor(time), new AuditTimestampInterceptor(time));
    }

    /// <summary>
    /// Attaches the SQL Server connectivity probe.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The name of the check.</param>
    /// <param name="tags">The tags that select the check.</param>
    /// <param name="timeout">The time after which the check fails.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IHealthChecksBuilder AddAndesMailerSqlHealthCheck(this IHealthChecksBuilder builder, string name, IEnumerable<string> tags, TimeSpan timeout) =>
        builder.AddCheck<SqlHealthCheck>(name, tags: tags, timeout: timeout);

    #endregion
}
