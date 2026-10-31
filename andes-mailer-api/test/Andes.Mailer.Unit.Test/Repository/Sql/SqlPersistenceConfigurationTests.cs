using Andes.Mailer.Repository.Sql;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Repository.Sql.Interceptors;
using Andes.Mailer.Repository.Sql.Options;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Andes.Mailer.Unit.Test.Repository.Sql;

public sealed class SqlPersistenceConfigurationTests
{
    private const string ConnectionString = "Server=localhost";

    [Fact]
    public void UseAndesMailerSqlServer_AnyOptions_RunsDeactivationBeforeAudit()
    {
        var interceptors = BuildOptions(retryOnFailure: true).FindExtension<CoreOptionsExtension>()?.Interceptors;

        Assert.NotNull(interceptors);
        Assert.Collection(
            interceptors,
            interceptor => Assert.IsType<DeactivationInterceptor>(interceptor),
            interceptor => Assert.IsType<AuditTimestampInterceptor>(interceptor));
    }

    [Fact]
    public void UseAndesMailerSqlServer_AnyOptions_TargetsSqlServer2025()
    {
        var options = BuildOptions(retryOnFailure: true);

        // The log fragment is the only public view of this setting; the extension that holds it is EF Core internal.
        Assert.Contains(options.Extensions, e => e.Info.LogFragment.Contains("SqlServerCompatibilityLevel=170", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UseAndesMailerSqlServer_RetryFlag_SetsExecutionStrategy(bool retryOnFailure)
    {
        using var ctx = new AndesMailerDbContext(BuildOptions(retryOnFailure));

        Assert.Equal(retryOnFailure, ctx.Database.CreateExecutionStrategy().RetriesOnFailure);
    }

    [Fact]
    public void UseAndesMailerSqlServer_NullTimeProvider_Throws()
    {
        var builder = new DbContextOptionsBuilder<AndesMailerDbContext>();

        Assert.Throws<ArgumentNullException>(() => builder.UseAndesMailerSqlServer(ConnectionString, null!, retryOnFailure: true));
    }

    [Fact]
    public void AddAndesMailerSqlPersistence_RegisteredContext_RetriesOnFailure()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SqlDbOptions.SectionName}:{nameof(SqlDbOptions.ConnectionString)}"] = ConnectionString,
            })
            .Build();
        using var provider = new ServiceCollection().AddAndesMailerSqlPersistence(configuration).BuildServiceProvider();
        using var scope = provider.CreateScope();

        var ctx = scope.ServiceProvider.GetRequiredService<AndesMailerDbContext>();

        Assert.True(ctx.Database.CreateExecutionStrategy().RetriesOnFailure);
    }

    #region Private methods

    private static DbContextOptions<AndesMailerDbContext> BuildOptions(bool retryOnFailure)
    {
        var builder = new DbContextOptionsBuilder<AndesMailerDbContext>();
        builder.UseAndesMailerSqlServer(ConnectionString, TimeProvider.System, retryOnFailure);

        return builder.Options;
    }

    #endregion
}
