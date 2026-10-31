using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Andes.Mailer.Common.Validation;

/// <summary>
/// Connects FluentValidation validators to the options pipeline.
/// </summary>
public static class FluentValidationOptionsBuilderExtensions
{
    #region Public static methods

    /// <summary>
    /// Validates the options with the registered <c>IValidator&lt;TOptions&gt;</c>.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <param name="builder">The options builder to extend.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static OptionsBuilder<TOptions> ValidateWithFluentValidation<TOptions>(this OptionsBuilder<TOptions> builder)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddSingleton<IValidateOptions<TOptions>>(services =>
            new FluentValidationOptionsValidator<TOptions>(builder.Name, services));

        return builder;
    }

    #endregion
}
