using Andes.Mailer.Entity.Base;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Andes.Mailer.Unit.Test.TestInfrastructure;

// Bridges the two SQL Server features the model relies on that SQLite lacks; real behaviour is covered by the integration suite.
// - rowversion: SQLite cannot generate one, so the NOT NULL column gets a random blob on insert. It never changes on update,
//   which still lets a wrong client version fail the concurrency check.
// - DateTimeOffset: SQLite stores it as text and can neither order nor compare it, so it is stored as a sortable 64-bit value instead.
public sealed class SqliteModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            if (entity.ClrType.IsAssignableTo(typeof(IBaseModifiedEntity)))
            {
                modelBuilder.Entity(entity.ClrType)
                    .Property(nameof(BaseModifiedEntity<>.Version))
                    .HasDefaultValueSql("randomblob(8)");
            }

            foreach (var property in entity.ClrType.GetProperties().Where(p => p.PropertyType == typeof(DateTimeOffset) || p.PropertyType == typeof(DateTimeOffset?)))
            {
                modelBuilder.Entity(entity.ClrType).Property(property.Name).HasConversion(new DateTimeOffsetToBinaryConverter());
            }
        }
    }
}
