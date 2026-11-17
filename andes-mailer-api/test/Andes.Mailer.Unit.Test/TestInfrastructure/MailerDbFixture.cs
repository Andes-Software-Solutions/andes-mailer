using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Entity.Emails.Templates;
using Andes.Mailer.Entity.Teams;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Repository.Sql.Interceptors;

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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
            _filePath = Path.Join(Path.GetTempPath(), $"andes-mailer-unit-{Guid.NewGuid():N}.db");
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

    // For tests that need to act at a precise point of a save, such as a race or a failure after the commit.
    public AndesMailerDbContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<AndesMailerDbContext>(_options).AddInterceptors(interceptors).Options);

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

    public async Task<Team> AddTeamAsync(string? name = null, string? serviceAccountEmail = null)
    {
        await using var ctx = CreateContext();
        var id = Guid.NewGuid();
        var team = new Team
        {
            Name = name ?? $"Team {id:N}",
            ServiceAccountEmail = serviceAccountEmail ?? $"mailbox-{id:N}@contoso.com",
            ServiceAccountId = Guid.NewGuid(),
        };
        ctx.Add(team);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return team;
    }

    public async Task<TeamMember> AddTeamMemberAsync(Guid teamId, Guid userId, Roles role = Roles.TeamMember)
    {
        await using var ctx = CreateContext();
        var member = new TeamMember { TeamId = teamId, UserId = userId, RoleId = role };
        ctx.Add(member);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return member;
    }

    public async Task<Email> AddEmailAsync(Guid teamId, Guid authorId, EmailStatuses status = EmailStatuses.Draft, params EmailAttachment[] attachments)
    {
        await using var ctx = CreateContext();
        var email = new Email { TeamId = teamId, AuthorId = authorId, Subject = "Report", BodyHtml = "<p>Hi</p>", StatusId = status };
        foreach (var attachment in attachments)
        {
            email.Attachments.Add(attachment);
        }

        ctx.Add(email);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return email;
    }

    public async Task<EmailTemplate> AddTemplateAsync(Guid teamId, Guid authorId, string? name = null, params EmailTemplateAttachment[] attachments)
    {
        await using var ctx = CreateContext();
        var template = new EmailTemplate
        {
            TeamId = teamId,
            AuthorId = authorId,
            Name = name ?? $"Template {Guid.NewGuid():N}",
            Subject = "Monthly report",
            BodyHtml = "<p>Hello</p>",
        };
        foreach (var attachment in attachments)
        {
            template.Attachments.Add(attachment);
        }

        ctx.Add(template);
        await ctx.SaveChangesAsync(TestContext.Current.CancellationToken);

        return template;
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
