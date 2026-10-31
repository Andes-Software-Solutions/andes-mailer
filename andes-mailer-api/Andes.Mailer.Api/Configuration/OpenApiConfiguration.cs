using Scalar.AspNetCore;

namespace Andes.Mailer.Api.Configuration;

internal static class OpenApiConfiguration
{
    #region Public static methods

    public static IServiceCollection AddAndesMailerOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi();

        return services;
    }

    public static WebApplication MapAndesMailerOpenApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        return app;
    }

    #endregion
}
