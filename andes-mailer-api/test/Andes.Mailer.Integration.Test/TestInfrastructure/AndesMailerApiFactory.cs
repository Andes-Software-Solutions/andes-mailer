using Andes.Mailer.Repository.Sql.Options;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Andes.Mailer.Integration.Test.TestInfrastructure;

public sealed class AndesMailerApiFactory(
    string connectionString,
    bool useTestAuthentication = false,
    IReadOnlyDictionary<string, string?>? configuration = null,
    Action<IServiceCollection>? configureTestServices = null) : WebApplicationFactory<Program>
{
    private const string TestEnvironment = "Testing";

    private readonly string _connectionString = connectionString;
    private readonly bool _useTestAuthentication = useTestAuthentication;
    private readonly IReadOnlyDictionary<string, string?> _configuration = configuration ?? new Dictionary<string, string?>();
    private readonly Action<IServiceCollection>? _configureTestServices = configureTestServices;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development: that loads user secrets, and the suite must never reach the local database.
        builder.UseEnvironment(TestEnvironment);
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(new Dictionary<string, string?>(_configuration)
        {
            [$"{SqlDbOptions.SectionName}:{nameof(SqlDbOptions.ConnectionString)}"] = _connectionString,
        }));

        if (_useTestAuthentication)
        {
            builder.ConfigureTestServices(services => services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { }));
        }

        if (_configureTestServices is not null)
        {
            builder.ConfigureTestServices(_configureTestServices);
        }
    }
}
