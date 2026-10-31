using Andes.Mailer.Integration.Test.TestInfrastructure;
using Andes.Mailer.Repository.Sql.Options;

using Microsoft.Extensions.Options;

namespace Andes.Mailer.Integration.Test.Startup;

public sealed class SqlOptionsStartupIntegrationTests
{
    [Fact]
    public async Task Start_EmptyConnectionString_RefusesToStart()
    {
        await using var factory = new AndesMailerApiFactory(string.Empty);

        var exception = Assert.Throws<OptionsValidationException>(() => factory.Server);

        Assert.Contains(nameof(SqlDbOptions.ConnectionString), exception.Message, StringComparison.Ordinal);
    }
}
