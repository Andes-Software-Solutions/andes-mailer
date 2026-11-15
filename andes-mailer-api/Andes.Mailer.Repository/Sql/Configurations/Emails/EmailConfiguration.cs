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
        builder.HasOne(e => e.Sender)
            .WithMany()
            .HasForeignKey(e => e.SenderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(e => e.Status)
            .WithMany()
            .HasForeignKey(e => e.StatusId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => new { e.SenderId, e.StatusId });

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
