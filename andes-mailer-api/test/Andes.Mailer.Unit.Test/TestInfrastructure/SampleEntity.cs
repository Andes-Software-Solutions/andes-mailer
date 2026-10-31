using Andes.Mailer.Entity.Base;

namespace Andes.Mailer.Unit.Test.TestInfrastructure;

public sealed class SampleEntity : BaseModifiedEntity<Guid>
{
    public required string Name { get; set; }
}
