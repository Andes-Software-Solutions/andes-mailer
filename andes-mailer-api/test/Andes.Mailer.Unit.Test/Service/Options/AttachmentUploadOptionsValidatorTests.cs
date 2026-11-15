using Andes.Mailer.Service.Options;

namespace Andes.Mailer.Unit.Test.Service.Options;

public sealed class AttachmentUploadOptionsValidatorTests
{
    private readonly AttachmentUploadOptionsValidator _validator = new();

    [Fact]
    public void Validate_Defaults_Pass()
    {
        Assert.True(_validator.Validate(new AttachmentUploadOptions()).IsValid);
    }

    [Fact]
    public void Validate_RelativeStagingPath_Fails()
    {
        var result = _validator.Validate(new AttachmentUploadOptions { StagingPath = "staging" });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AttachmentUploadOptions.StagingPath));
    }

    [Fact]
    public void Validate_EmailCapBelowFileCap_Fails()
    {
        var result = _validator.Validate(new AttachmentUploadOptions { MaxFileBytes = 10, MaxEmailBytes = 5 });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AttachmentUploadOptions.MaxEmailBytes));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(65)]
    public void Validate_ConcurrencyOutOfRange_Fails(int concurrency)
    {
        var result = _validator.Validate(new AttachmentUploadOptions { MaxConcurrency = concurrency });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AttachmentUploadOptions.MaxConcurrency));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Validate_UploadAttemptsOutOfRange_Fails(int attempts)
    {
        var result = _validator.Validate(new AttachmentUploadOptions { MaxUploadAttempts = attempts });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(AttachmentUploadOptions.MaxUploadAttempts));
    }

    [Fact]
    public void ResolvedStagingPath_Empty_UsesTempDirectory()
    {
        var path = new AttachmentUploadOptions().ResolvedStagingPath;

        Assert.StartsWith(Path.GetTempPath(), path, StringComparison.OrdinalIgnoreCase);
    }
}
