namespace Andes.Mailer.Api.Middleware;

internal static class RequestLoggingRegistration
{
    #region Public static methods

    public static IApplicationBuilder UseAndesMailerRequestLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestLoggingMiddleware>();

    #endregion
}
