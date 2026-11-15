using Andes.Mailer.Integration.Test.TestInfrastructure;
using Andes.Mailer.Repository.Blob.Options;
using Andes.Mailer.Service.Options;

using Microsoft.Extensions.Options;

namespace Andes.Mailer.Integration.Test.Startup;

public sealed class BlobOptionsStartupIntegrationTests(SqlServerFixture fixture)
{
    private readonly SqlServerFixture _fixture = fixture;

    [Fact]
    public async Task Start_EmptyBlobConnectionString_RefusesToStart()
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString, string.Empty);

        var exception = Assert.Throws<OptionsValidationException>(() => factory.Server);

        Assert.Contains(nameof(BlobStorageOptions.ConnectionString), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Start_RelativeStagingPath_RefusesToStart()
    {
        await using var factory = new AndesMailerApiFactory(
            _fixture.ConnectionString,
            "UseDevelopmentStorage=true",
            configuration: new Dictionary<string, string?>
            {
                [$"{AttachmentUploadOptions.SectionName}:{nameof(AttachmentUploadOptions.StagingPath)}"] = "relative/staging",
            });

        var exception = Assert.Throws<OptionsValidationException>(() => factory.Server);

        Assert.Contains(nameof(AttachmentUploadOptions.StagingPath), exception.Message, StringComparison.Ordinal);
    }
}
