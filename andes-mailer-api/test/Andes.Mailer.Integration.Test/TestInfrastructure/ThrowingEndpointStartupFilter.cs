using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Andes.Mailer.Integration.Test.TestInfrastructure;

// The scaffold has no endpoint that can throw; mapping this after next keeps it inside the app's exception handler.
public sealed class ThrowingEndpointStartupFilter : IStartupFilter
{
    public const string Path = "/test/throw";
    public const string ExceptionMessage = "Internal detail that must not reach the client.";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.Map(Path, branch => branch.Run(_ => throw new InvalidOperationException(ExceptionMessage)));
    };
}
