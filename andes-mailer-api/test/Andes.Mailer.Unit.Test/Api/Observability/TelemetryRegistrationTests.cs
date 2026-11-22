using Andes.Mailer.Api.Observability;
using Andes.Mailer.Api.Options;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Andes.Mailer.Unit.Test.Api.Observability;

public sealed class TelemetryRegistrationTests
{
    private const string ConnectionString =
        "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westeurope-5.in.applicationinsights.azure.com/";

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("IngestionEndpoint=https://westeurope-5.in.applicationinsights.azure.com/")]
    public void AddAndesMailerTelemetry_MalformedConnectionStringVariable_ThrowsNamingTheVariable(string value)
    {
        var configuration = Configuration(new() { [ApplicationInsightsOptions.ConnectionStringVariable] = value });

        var exception = Assert.Throws<OptionsValidationException>(() => new ServiceCollection().AddAndesMailerTelemetry(configuration));

        Assert.All(exception.Failures, failure => Assert.StartsWith(ApplicationInsightsOptions.ConnectionStringVariable, failure, StringComparison.Ordinal));
    }

    [Fact]
    public void AddAndesMailerTelemetry_WellFormedConnectionStringVariable_Registers()
    {
        var configuration = Configuration(new() { [ApplicationInsightsOptions.ConnectionStringVariable] = ConnectionString });

        var services = new ServiceCollection().AddAndesMailerTelemetry(configuration);

        Assert.NotEmpty(services);
    }

    [Fact]
    public void AddAndesMailerTelemetry_SectionSet_IgnoresAMalformedVariable()
    {
        var configuration = Configuration(new()
        {
            [$"{ApplicationInsightsOptions.SectionName}:{nameof(ApplicationInsightsOptions.ConnectionString)}"] = ConnectionString,
            [ApplicationInsightsOptions.ConnectionStringVariable] = "not a connection string",
        });

        var services = new ServiceCollection().AddAndesMailerTelemetry(configuration);

        Assert.NotEmpty(services);
    }

    #region Private methods

    private static IConfiguration Configuration(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

    #endregion
}
