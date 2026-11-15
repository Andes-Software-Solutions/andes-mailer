using Andes.Mailer.Repository.Blob.Options;
using Andes.Mailer.Repository.Sql.Options;
using Andes.Mailer.Service.Options;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Andes.Mailer.Integration.Test.TestInfrastructure;

public sealed class AndesMailerApiFactory(
    string connectionString,
    string blobConnectionString,
    bool useTestAuthentication = false,
    IReadOnlyDictionary<string, string?>? configuration = null,
    Action<IServiceCollection>? configureTestServices = null) : WebApplicationFactory<Program>
{
    private const string TestEnvironment = "Testing";

    private readonly string _connectionString = connectionString;
    private readonly string _blobConnectionString = blobConnectionString;
    private readonly bool _useTestAuthentication = useTestAuthentication;
    private readonly IReadOnlyDictionary<string, string?> _configuration = configuration ?? new Dictionary<string, string?>();
    private readonly Action<IServiceCollection>? _configureTestServices = configureTestServices;

    // Per factory, so hosts running side by side never recover or sweep each other's staged uploads.
    private readonly string _stagingPath = Path.Combine(Path.GetTempPath(), "andes-mailer-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Not Development: that loads user secrets, and the suite must never reach the local database.
        builder.UseEnvironment(TestEnvironment);

        // The caller's entries are applied last so a test can override any default.
        var settings = new Dictionary<string, string?>
        {
            [$"{SqlDbOptions.SectionName}:{nameof(SqlDbOptions.ConnectionString)}"] = _connectionString,
            [$"{BlobStorageOptions.SectionName}:{nameof(BlobStorageOptions.ConnectionString)}"] = _blobConnectionString,
            [$"{AttachmentUploadOptions.SectionName}:{nameof(AttachmentUploadOptions.StagingPath)}"] = _stagingPath,

            // Hosts in the suite run side by side on one database, like several instances; recovery in one would fail another's uploads.
            [$"{AttachmentUploadOptions.SectionName}:{nameof(AttachmentUploadOptions.RecoverOnStartup)}"] = "false",
        };
        foreach (var (key, value) in _configuration)
        {
            settings[key] = value;
        }

        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(settings));

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

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (Directory.Exists(_stagingPath))
        {
            Directory.Delete(_stagingPath, recursive: true);
        }
    }
}
