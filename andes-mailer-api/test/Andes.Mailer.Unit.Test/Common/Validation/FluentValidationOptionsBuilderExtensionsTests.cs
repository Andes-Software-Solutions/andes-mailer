using Andes.Mailer.Common.Validation;
using Andes.Mailer.Unit.Test.TestInfrastructure;

using FluentValidation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Andes.Mailer.Unit.Test.Common.Validation;

public sealed class FluentValidationOptionsBuilderExtensionsTests
{
    private const string OptionsName = "named";
    private const string OtherOptionsName = "other";

    [Fact]
    public void ValidateWithFluentValidation_ValidOptions_ReturnsValue()
    {
        using var provider = BuildProvider(OptionsName, "set");

        var options = provider.GetRequiredService<IOptionsMonitor<SampleOptions>>().Get(OptionsName);

        Assert.Equal("set", options.Value);
    }

    [Fact]
    public void ValidateWithFluentValidation_InvalidOptions_ThrowsNamingTypeAndProperty()
    {
        using var provider = BuildProvider(OptionsName, string.Empty);

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptionsMonitor<SampleOptions>>().Get(OptionsName));

        Assert.Contains($"{nameof(SampleOptions)}.{nameof(SampleOptions.Value)}:", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateWithFluentValidation_OtherOptionsName_SkipsValidation()
    {
        using var provider = BuildProvider(OptionsName, string.Empty);

        var options = provider.GetRequiredService<IOptionsMonitor<SampleOptions>>().Get(OtherOptionsName);

        Assert.Empty(options.Value);
    }

    #region Private methods

    private static ServiceProvider BuildProvider(string name, string value)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IValidator<SampleOptions>, SampleOptionsValidator>();
        services.AddOptions<SampleOptions>(name)
            .Configure(o => o.Value = value)
            .ValidateWithFluentValidation();

        return services.BuildServiceProvider();
    }

    #endregion
}
