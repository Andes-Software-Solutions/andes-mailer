using Andes.Mailer.Api.Idempotency;
using Andes.Mailer.Api.Options;
using Andes.Mailer.Common.Validation;

using FluentValidation;

namespace Andes.Mailer.Api.Configuration;

internal static class IdempotencyConfiguration
{
    #region Public static methods

    public static IServiceCollection AddAndesMailerIdempotency(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidator<IdempotencyOptions>, IdempotencyOptionsValidator>();
        services.AddOptions<IdempotencyOptions>()
            .Bind(configuration.GetSection(IdempotencyOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        // TimeProvider comes from AddAndesMailerSqlPersistence, which registers the one clock the app shares.
        services.AddSingleton<IIdempotencyStore, IdempotencyStore>();

        return services;
    }

    #endregion
}
