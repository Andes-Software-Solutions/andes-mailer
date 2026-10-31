using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Unit.Test.TestInfrastructure;

public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
    public DbSet<SampleEntity> Samples => Set<SampleEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfiguration(new SampleEntityConfiguration());
}
