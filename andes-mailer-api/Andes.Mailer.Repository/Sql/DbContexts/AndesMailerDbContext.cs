using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Entity.Users;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Repository.Sql.DbContexts;

/// <summary>
/// Represents the SQL Server store.
/// </summary>
/// <param name="options">The options configured by the persistence registration.</param>
public sealed class AndesMailerDbContext(DbContextOptions<AndesMailerDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Gets the users.
    /// </summary>
    public DbSet<User> Users => Set<User>();

    /// <summary>
    /// Gets the roles granted to users.
    /// </summary>
    public DbSet<UserRole> UserRoles => Set<UserRole>();

    /// <summary>
    /// Gets the permissions granted to roles.
    /// </summary>
    public DbSet<RoleGrant> RoleGrants => Set<RoleGrant>();

    /// <summary>
    /// Gets the emails.
    /// </summary>
    public DbSet<Email> Emails => Set<Email>();

    /// <summary>
    /// Gets the recipients of emails.
    /// </summary>
    public DbSet<EmailRecipient> EmailRecipients => Set<EmailRecipient>();

    /// <summary>
    /// Gets the attachments of emails.
    /// </summary>
    public DbSet<EmailAttachment> EmailAttachments => Set<EmailAttachment>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AndesMailerDbContext).Assembly);
}
