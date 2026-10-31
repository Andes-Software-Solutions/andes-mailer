using Andes.Mailer.Repository.Sql.Options;

namespace Andes.Mailer.Unit.Test.Repository.Sql.Options;

public sealed class SqlDbOptionsValidatorTests
{
    private readonly SqlDbOptionsValidator _validator = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankConnectionString_Fails(string connectionString)
    {
        var result = _validator.Validate(new SqlDbOptions { ConnectionString = connectionString });

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_ConnectionStringSet_Passes()
    {
        var result = _validator.Validate(new SqlDbOptions { ConnectionString = "Server=localhost" });

        Assert.True(result.IsValid);
    }
}
