using Andes.Mailer.Repository.Sql.Configurations.Base;

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Andes.Mailer.Unit.Test.TestInfrastructure;

public sealed class SampleEntityConfiguration : BaseModifiedEntityConfiguration<SampleEntity, Guid>
{
    // SQLite cannot generate a rowversion, and these tests don't exercise concurrency.
    protected override void ConfigureEntity(EntityTypeBuilder<SampleEntity> builder) =>
        builder.Ignore(e => e.Version);
}
