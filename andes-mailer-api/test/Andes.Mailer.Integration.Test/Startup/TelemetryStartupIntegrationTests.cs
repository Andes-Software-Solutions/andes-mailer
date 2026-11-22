using Andes.Mailer.Integration.Test.TestInfrastructure;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using OpenTelemetry.Instrumentation.AspNetCore;

namespace Andes.Mailer.Integration.Test.Startup;

public sealed class TelemetryStartupIntegrationTests(SqlServerFixture fixture, AzuriteFixture azurite)
{
    private readonly SqlServerFixture _fixture = fixture;
    private readonly AzuriteFixture _azurite = azurite;

    [Theory]
    [InlineData("/health/live", false)]
    [InlineData("/health/ready", false)]
    [InlineData("/api/users/me", true)]
    public async Task RequestTracing_Path_TracesEverythingButHealthProbes(string path, bool traced)
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString, _azurite.ConnectionString);
        var options = factory.Services.GetRequiredService<IOptionsMonitor<AspNetCoreTraceInstrumentationOptions>>().CurrentValue;
        var context = new DefaultHttpContext();
        context.Request.Path = path;

        var filter = Assert.IsType<Func<HttpContext, bool>>(options.Filter);

        Assert.Equal(traced, filter(context));
    }
}
