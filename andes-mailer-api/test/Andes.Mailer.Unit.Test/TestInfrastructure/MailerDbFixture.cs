using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Repository.Sql.Interceptors;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Time.Testing;

namespace Andes.Mailer.Unit.Test.TestInfrastructure;

// SQLite instead of Testcontainers keeps the Unit.Test suite free of Docker. Schemas are ignored and rowversion is emulated
// (SqliteRowVersionModelCustomizer); behaviour that depends on SQL Server is covered by the integration suite.
public sealed class MailerDbFixture : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<AndesMailerDbContext> _options;

    public MailerDbFixture()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<AndesMailerDbContext>()
            .UseSqlite(_connection)
            .ReplaceService<IModelCustomizer, SqliteRowVersionModelCustomizer>()
            .AddInterceptors(new DeactivationInterceptor(Time), new AuditTimestampInterceptor(Time))
            .Options;

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
    }

    public FakeTimeProvider Time { get; } = new();

    public AndesMailerDbContext CreateContext() => new(_options);

    public async Task<User> AddUserAsync(string email, string firstName = "Ada", string lastName = "Lovelace", Roles? role = null)
    {
        await using var ctx = CreateContext();
        var user = new User { Id = Guid.NewGuid(), FirstName = firstName, LastName = lastName, Email = email };
        ctx.Add(user);
        if (role is { } granted)
        {
            ctx.Add(new UserRole { UserId = user.Id, RoleId = granted });
        }

        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return user;
    }

    public async Task DeactivateUserAsync(Guid id)
    {
        await using var ctx = CreateContext();
        var user = await ctx.Users.SingleAsync(u => u.Id == id, TestContext.Current.CancellationToken);
        ctx.Remove(user);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public void Dispose() => _connection.Dispose();
}
