using Andes.Mailer.Repository.Sql.Options;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Andes.Mailer.Integration.Test.TestInfrastructure;

public sealed class AndesMailerApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    private const string TestEnvironment = "Testing";

    private readonly string _connectionString = connectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development: that loads user secrets, and the suite must never reach the local database.
        builder.UseEnvironment(TestEnvironment);
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [$"{SqlDbOptions.SectionName}:{nameof(SqlDbOptions.ConnectionString)}"] = _connectionString,
        }));
    }
}
