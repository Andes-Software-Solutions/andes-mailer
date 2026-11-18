using Andes.Mailer.Entity.ContactGroups;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Entity.Emails.Templates;
using Andes.Mailer.Entity.Teams;
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
    /// Gets the teams.
    /// </summary>
    public DbSet<Team> Teams => Set<Team>();

    /// <summary>
    /// Gets the memberships of users in teams.
    /// </summary>
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    /// <summary>
    /// Gets the teams' contact groups.
    /// </summary>
    public DbSet<ContactGroup> ContactGroups => Set<ContactGroup>();

    /// <summary>
    /// Gets the members of the contact groups.
    /// </summary>
    public DbSet<ContactGroupMember> ContactGroupMembers => Set<ContactGroupMember>();

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

    /// <summary>
    /// Gets the email templates.
    /// </summary>
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();

    /// <summary>
    /// Gets the recipients of email templates.
    /// </summary>
    public DbSet<EmailTemplateRecipient> EmailTemplateRecipients => Set<EmailTemplateRecipient>();

    /// <summary>
    /// Gets the attachments of email templates.
    /// </summary>
    public DbSet<EmailTemplateAttachment> EmailTemplateAttachments => Set<EmailTemplateAttachment>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AndesMailerDbContext).Assembly);
}
