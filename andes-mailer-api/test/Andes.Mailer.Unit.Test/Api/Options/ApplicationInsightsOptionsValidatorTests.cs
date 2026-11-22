using Andes.Mailer.Api.Options;

namespace Andes.Mailer.Unit.Test.Api.Options;

public sealed class ApplicationInsightsOptionsValidatorTests
{
    private const string ConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope-5.in.applicationinsights.azure.com/";

    private readonly ApplicationInsightsOptionsValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(ConnectionString)]
    public void Validate_ConnectionStringMissingOrWellFormed_Passes(string? connectionString)
    {
        var result = _validator.Validate(new ApplicationInsightsOptions { ConnectionString = connectionString });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("IngestionEndpoint=https://westeurope-5.in.applicationinsights.azure.com/")]
    public void Validate_ConnectionStringWithoutInstrumentationKey_Fails(string connectionString)
    {
        var result = _validator.Validate(new ApplicationInsightsOptions { ConnectionString = connectionString });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ApplicationInsightsOptions.ConnectionString));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    public void Validate_TracesPerSecondOutOfRange_Fails(double tracesPerSecond)
    {
        var result = _validator.Validate(new ApplicationInsightsOptions { TracesPerSecond = tracesPerSecond });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(ApplicationInsightsOptions.TracesPerSecond));
    }
}
