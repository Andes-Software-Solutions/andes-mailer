using Andes.Mailer.Integration.Test.TestInfrastructure;

using Testcontainers.Azurite;

[assembly: AssemblyFixture(typeof(AzuriteFixture))]

namespace Andes.Mailer.Integration.Test.TestInfrastructure;

// Every host the suite starts provisions and probes blob storage, so one emulator serves the whole assembly.
public sealed class AzuriteFixture : IAsyncLifetime
{
    // Pinned rather than latest so an emulator release can't change a test run unannounced.
    private const string Image = "mcr.microsoft.com/azure-storage/azurite:3.37.0";

    // The Blob SDK sends a newer service version than the emulator release knows; the emulator's own switch accepts it.
    private readonly AzuriteContainer _container = new AzuriteBuilder(Image)
        .WithInMemoryPersistence()
        .WithCommand("--skipApiVersionCheck")
        .Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync(TestContext.Current.CancellationToken);
        ConnectionString = _container.GetConnectionString();
    }

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();
}
