using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Repository.Sql.Interceptors;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Time.Testing;

namespace Andes.Mailer.Unit.Test.TestInfrastructure;

// SQLite instead of Testcontainers keeps the Unit.Test suite free of Docker. Schemas are ignored and rowversion is emulated
// (SqliteModelCustomizer), and DateTimeOffset is stored as a number; behaviour that depends on SQL Server is covered by the integration suite.
public sealed class MailerDbFixture : IDisposable
{
    private readonly SqliteConnection? _connection;
    private readonly string? _filePath;
    private readonly DbContextOptions<AndesMailerDbContext> _options;

    // concurrent: for code that runs contexts on several threads at once, such as a background service. A file database gives each
    // context its own connection, because one shared in-memory connection is not thread-safe.
    public MailerDbFixture(bool concurrent = false)
    {
        var builder = new DbContextOptionsBuilder<AndesMailerDbContext>();
        if (concurrent)
        {
            _filePath = Path.Combine(Path.GetTempPath(), $"andes-mailer-unit-{Guid.NewGuid():N}.db");
            builder.UseSqlite($"Data Source={_filePath}");
        }
        else
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            builder.UseSqlite(_connection);
        }

        _options = builder
            .ReplaceService<IModelCustomizer, SqliteModelCustomizer>()
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

    public async Task<Email> AddEmailAsync(Guid senderId, EmailStatuses status = EmailStatuses.Draft, params EmailAttachment[] attachments)
    {
        await using var ctx = CreateContext();
        var email = new Email { SenderId = senderId, Subject = "Report", BodyHtml = "<p>Hi</p>", StatusId = status };
        foreach (var attachment in attachments)
        {
            email.Attachments.Add(attachment);
        }

        ctx.Add(email);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return email;
    }

    public async Task DeactivateUserAsync(Guid id)
    {
        await using var ctx = CreateContext();
        var user = await ctx.Users.SingleAsync(u => u.Id == id, TestContext.Current.CancellationToken);
        ctx.Remove(user);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public void Dispose()
    {
        _connection?.Dispose();
        if (_filePath is not null)
        {
            // Pooled connections keep the file open on Windows.
            SqliteConnection.ClearAllPools();
            File.Delete(_filePath);
        }
    }
}
