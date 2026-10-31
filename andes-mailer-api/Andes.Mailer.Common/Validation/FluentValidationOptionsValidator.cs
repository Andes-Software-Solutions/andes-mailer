using FluentValidation;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Andes.Mailer.Common.Validation;

internal sealed class FluentValidationOptionsValidator<TOptions>(string? name, IServiceProvider services) : IValidateOptions<TOptions>
    where TOptions : class
{
    private readonly string? _name = name;
    private readonly IServiceProvider _services = services;

    public ValidateOptionsResult Validate(string? name, TOptions options)
    {
        if (_name is not null && _name != name)
        {
            return ValidateOptionsResult.Skip;
        }

        ArgumentNullException.ThrowIfNull(options);

        using var scope = _services.CreateScope();
        var validator = scope.ServiceProvider.GetRequiredService<IValidator<TOptions>>();
        var result = validator.Validate(options);
        if (result.IsValid)
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(result.Errors.Select(e => $"{typeof(TOptions).Name}.{e.PropertyName}: {e.ErrorMessage}"));
    }
}
