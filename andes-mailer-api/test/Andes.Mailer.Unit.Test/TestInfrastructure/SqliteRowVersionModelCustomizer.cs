using Andes.Mailer.Entity.Base;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Andes.Mailer.Unit.Test.TestInfrastructure;

// SQLite cannot generate a rowversion, so the NOT NULL column gets a random blob on insert. It never changes on update,
// which still lets a wrong client version fail the concurrency check; real rowversion behaviour is covered by the integration suite.
public sealed class SqliteRowVersionModelCustomizer(ModelCustomizerDependencies dependencies) : RelationalModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        foreach (var entity in modelBuilder.Model.GetEntityTypes().Where(e => e.ClrType.IsAssignableTo(typeof(IBaseModifiedEntity))))
        {
            modelBuilder.Entity(entity.ClrType)
                .Property(nameof(BaseModifiedEntity<>.Version))
                .HasDefaultValueSql("randomblob(8)");
        }
    }
}
