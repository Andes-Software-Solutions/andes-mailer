using Andes.Mailer.Api.Options;
using Andes.Mailer.Common.Constants;
using Andes.Mailer.Common.Validation;

using FluentValidation;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Identity.Web;

namespace Andes.Mailer.Api.Configuration;

internal static class AuthenticationConfiguration
{
    #region Public static methods

    public static IServiceCollection AddAndesMailerAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(AzureAdOptions.SectionName);

        services.AddSingleton<IValidator<AzureAdOptions>, AzureAdOptionsValidator>();
        services.AddOptions<AzureAdOptions>()
            .Bind(section)
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddMicrosoftIdentityWebApi(section);

        var accessAsUser = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireScope(ApiScopes.AccessAsUser)
            .Build();

        // Also the fallback, so an endpoint is protected even without RequireAuthorization; public routes opt out.
        services.AddAuthorizationBuilder()
            .SetDefaultPolicy(accessAsUser)
            .SetFallbackPolicy(accessAsUser);

        return services;
    }

    #endregion
}
