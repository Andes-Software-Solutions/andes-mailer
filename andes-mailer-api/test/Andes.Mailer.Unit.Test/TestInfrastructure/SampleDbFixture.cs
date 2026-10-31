using Andes.Mailer.Repository.Sql.Interceptors;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Andes.Mailer.Unit.Test.TestInfrastructure;

// SQLite instead of Testcontainers: the interceptors only read the change tracker, and the Unit.Test suite stays free of Docker.
public sealed class SampleDbFixture : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<SampleDbContext> _options;

    public SampleDbFixture()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new DeactivationInterceptor(Time), new AuditTimestampInterceptor(Time))
            .Options;

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    public FakeTimeProvider Time { get; } = new();

    public SampleDbContext CreateContext() => new(_options);

    public void Dispose() => _connection.Dispose();
}
