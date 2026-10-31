using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Repository.Sql.DbContexts;

/// <summary>
/// Represents the SQL Server store.
/// </summary>
/// <param name="options">The options configured by the persistence registration.</param>
public sealed class AndesMailerDbContext(DbContextOptions<AndesMailerDbContext> options) : DbContext(options)
{
    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AndesMailerDbContext).Assembly);
}
