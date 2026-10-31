using System.Diagnostics;

using Andes.Mailer.Api.ExceptionHandlers;

namespace Andes.Mailer.Api.Problems;

internal static class ProblemDetailsRegistration
{
    #region Public static methods

    public static IServiceCollection AddAndesMailerProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            // The path alone: RFC 9457 makes instance a URI reference, and a query string can carry PII.
            context.ProblemDetails.Instance ??= context.HttpContext.Request.Path.ToUriComponent();
            context.ProblemDetails.Extensions.TryAdd("traceId", Activity.Current?.Id ?? context.HttpContext.TraceIdentifier);
        });
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    #endregion
}
