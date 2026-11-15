using Andes.Mailer.Repository.Blob.Options;

namespace Andes.Mailer.Unit.Test.Repository.Blob.Options;

public sealed class BlobStorageOptionsValidatorTests
{
    private readonly BlobStorageOptionsValidator _validator = new();

    [Fact]
    public void Validate_ConnectionStringAndDefaultContainer_Passes()
    {
        var result = _validator.Validate(new BlobStorageOptions { ConnectionString = "UseDevelopmentStorage=true" });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_BlankConnectionString_Fails(string connectionString)
    {
        var result = _validator.Validate(new BlobStorageOptions { ConnectionString = connectionString });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BlobStorageOptions.ConnectionString));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("Attachments")]
    [InlineData("-attachments")]
    [InlineData("attachments-")]
    [InlineData("email--attachments")]
    [InlineData("email_attachments")]
    public void Validate_InvalidContainerName_Fails(string container)
    {
        var result = _validator.Validate(new BlobStorageOptions { ConnectionString = "UseDevelopmentStorage=true", AttachmentsContainer = container });

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(BlobStorageOptions.AttachmentsContainer));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("email-attachments-2026")]
    public void Validate_ValidContainerName_Passes(string container)
    {
        var result = _validator.Validate(new BlobStorageOptions { ConnectionString = "UseDevelopmentStorage=true", AttachmentsContainer = container });

        Assert.True(result.IsValid);
    }
}
