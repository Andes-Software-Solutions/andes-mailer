using Andes.Mailer.Api.Options;

namespace Andes.Mailer.Unit.Test.Api.Options;

public sealed class AzureAdOptionsValidatorTests
{
    private const string Instance = "https://login.microsoftonline.com/";
    private const string TenantId = "bdeeb99b-f1e9-443f-b7ff-1426199689dc";
    private const string ClientId = "b40defa0-5309-45c4-82fc-cb284010cc10";

    private readonly AzureAdOptionsValidator _validator = new();

    [Fact]
    public void Validate_AllSettingsValid_Passes()
    {
        var result = _validator.Validate(new AzureAdOptions { Instance = Instance, TenantId = TenantId, ClientId = ClientId });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("login.microsoftonline.com")]
    [InlineData("http://login.microsoftonline.com/")]
    public void Validate_InstanceNotAbsoluteHttps_Fails(string instance)
    {
        var result = _validator.Validate(new AzureAdOptions { Instance = instance, TenantId = TenantId, ClientId = ClientId });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AzureAdOptions.Instance));
    }

    [Theory]
    [InlineData("")]
    [InlineData("common")]
    public void Validate_TenantIdNotGuid_Fails(string tenantId)
    {
        var result = _validator.Validate(new AzureAdOptions { Instance = Instance, TenantId = tenantId, ClientId = ClientId });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AzureAdOptions.TenantId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-guid")]
    public void Validate_ClientIdNotGuid_Fails(string clientId)
    {
        var result = _validator.Validate(new AzureAdOptions { Instance = Instance, TenantId = TenantId, ClientId = clientId });

        Assert.Contains(result.Errors, error => error.PropertyName == nameof(AzureAdOptions.ClientId));
    }
}
