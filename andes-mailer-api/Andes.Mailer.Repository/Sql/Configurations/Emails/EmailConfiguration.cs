using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Emails;

/// <summary>
/// Maps <see cref="Email"/> to <c>[Core].[Email]</c>.
/// </summary>
public sealed class EmailConfiguration : BaseModifiedEntityConfiguration<Email, Guid>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<Email> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("Email", SqlSchemas.Core);
        builder.Property(e => e.Subject).HasMaxLength(EmailLimits.SubjectMaxLength).IsRequired();

        // nvarchar(max): the validator caps the length, and a sized column would top out at 4,000 characters.
        builder.Property(e => e.BodyHtml).IsRequired();
        builder.HasOne(e => e.Team)
            .WithMany()
            .HasForeignKey(e => e.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Author)
            .WithMany()
            .HasForeignKey(e => e.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Status)
            .WithMany()
            .HasForeignKey(e => e.StatusId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Template)
            .WithMany()
            .HasForeignKey(e => e.TemplateId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.TeamId, e.StatusId });
        builder.HasIndex(e => e.AuthorId);
        builder.HasIndex(e => e.TemplateId);

        // Beside the row's own deactivation filter, so a deactivated team's emails stay hidden with it.
        builder.HasQueryFilter("TeamDeactivation", e => e.Team!.DateDeactivated == null);

        builder.HasMany(e => e.Recipients)
            .WithOne(r => r.Email)
            .HasForeignKey(r => r.EmailId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Attachments)
            .WithOne(a => a.Email)
            .HasForeignKey(a => a.EmailId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
