using Andes.Mailer.Common.Constants.Users;
using Andes.Mailer.Entity.Users;
using Andes.Mailer.Repository.Sql.Configurations.Base;
using Andes.Mailer.Repository.Sql.Constants;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Repository.Sql.Configurations.Users;

/// <summary>
/// Maps <see cref="User"/> to <c>[Core].[User]</c>.
/// </summary>
public sealed class UserConfiguration : BaseModifiedEntityConfiguration<User, Guid>
{
    /// <inheritdoc/>
    protected override void ConfigureEntity(EntityTypeBuilder<User> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("User", SqlSchemas.Core);

        // The key is the Entra object id, so a generated value would never match a token's oid claim.
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.FirstName).HasMaxLength(UserLimits.FirstNameMaxLength).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(UserLimits.LastNameMaxLength).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(UserLimits.EmailMaxLength).IsRequired();

        // Active rows only, so a deactivated row never blocks an address now held by another directory account.
        builder.HasIndex(u => u.Email).IsUnique().HasFilter("[DateDeactivated] IS NULL");
        builder.Ignore(u => u.FullName);

        builder.HasMany(u => u.UserRoles)
            .WithOne(ur => ur.User)
            .HasForeignKey(ur => ur.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
