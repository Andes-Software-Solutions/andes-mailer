using Andes.Mailer.Api.Options;
using Andes.Mailer.Integration.Test.TestInfrastructure;

using Microsoft.Extensions.Options;

namespace Andes.Mailer.Integration.Test.Startup;

public sealed class AzureAdOptionsStartupIntegrationTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Start_EmptyClientId_RefusesToStart()
    {
        await using var factory = new AndesMailerApiFactory(
            _fixture.ConnectionString,
            configuration: new Dictionary<string, string?>
            {
                [$"{AzureAdOptions.SectionName}:{nameof(AzureAdOptions.ClientId)}"] = string.Empty,
            });

        var exception = Assert.Throws<OptionsValidationException>(() => factory.Server);

        Assert.Contains(nameof(AzureAdOptions.ClientId), exception.Message, StringComparison.Ordinal);
    }
}
