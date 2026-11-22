namespace Andes.Mailer.Api.Middleware;

internal static class RequestContextRegistration
{
    #region Public static methods

    public static IApplicationBuilder UseAndesMailerRequestContext(this IApplicationBuilder app) =>
        app.UseMiddleware<RequestContextMiddleware>();

    #endregion
}
