using Andes.Mailer.Integration.Test.TestInfrastructure;
using Andes.Mailer.Repository.Sql;
using Andes.Mailer.Repository.Sql.DbContexts;

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

using Testcontainers.MsSql;

[assembly: AssemblyFixture(typeof(SqlServerFixture))]

namespace Andes.Mailer.Integration.Test.TestInfrastructure;

public sealed class SqlServerFixture : IAsyncLifetime
{
    // Pinned to a build rather than 2025-latest so a new cumulative update can't change a test run unannounced.
    private const string Image = "mcr.microsoft.com/mssql/server:2025-CU9-ubuntu-24.04";
    private const string DatabaseName = "AndesMailer";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _container.StartAsync(cancellationToken);
        ConnectionString = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = DatabaseName,
        }.ConnectionString;

        await using var ctx = CreateContext();
        await ctx.GetService<IRelationalDatabaseCreator>().CreateAsync(cancellationToken);
        if (ctx.Database.GetMigrations().Any())
        {
            await ctx.Database.MigrateAsync(cancellationToken);
        }
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    public AndesMailerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AndesMailerDbContext>();
        options.UseAndesMailerSqlServer(ConnectionString, TimeProvider.System, retryOnFailure: false);

        return new AndesMailerDbContext(options.Options);
    }
}
