using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Entity.Emails.Templates;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Emails.Templates;

/// <summary>
/// Maps <see cref="EmailTemplate"/> to <c>[Core].[EmailTemplate]</c>.
/// </summary>
public sealed class EmailTemplateConfiguration : BaseModifiedEntityConfiguration<EmailTemplate, Guid>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<EmailTemplate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("EmailTemplate", SqlSchemas.Core);
        builder.Property(t => t.Name).HasMaxLength(EmailTemplateLimits.NameMaxLength).IsRequired();
        builder.Property(t => t.Subject).HasMaxLength(EmailLimits.SubjectMaxLength).IsRequired();

        // nvarchar(max): the validator caps the length, and a sized column would top out at 4,000 characters.
        builder.Property(t => t.BodyHtml).IsRequired();
        builder.HasOne(t => t.Team)
            .WithMany()
            .HasForeignKey(t => t.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(t => t.Author)
            .WithMany()
            .HasForeignKey(t => t.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);

        // Active rows only, so a deactivated template never blocks its name from being used again.
        builder.HasIndex(t => new { t.TeamId, t.Name }).IsUnique().HasFilter("[DateDeactivated] IS NULL");
        builder.HasIndex(t => t.AuthorId);

        // Beside the row's own deactivation filter, so a deactivated team's templates stay hidden with it.
        builder.HasQueryFilter("TeamDeactivation", t => t.Team!.DateDeactivated == null);

        builder.HasMany(t => t.Recipients)
            .WithOne(r => r.Template)
            .HasForeignKey(r => r.TemplateId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(t => t.Attachments)
            .WithOne(a => a.Template)
            .HasForeignKey(a => a.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
