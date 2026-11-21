using System.Diagnostics;

using Microsoft.AspNetCore.Http.Features;

namespace Andes.Mailer.Unit.Test.TestInfrastructure;

/// <summary>
/// Hands a test's activity to code that reads the request activity, as the ASP.NET Core host does for a real request.
/// </summary>
internal sealed class FakeHttpActivityFeature(Activity activity) : IHttpActivityFeature
{
    public Activity Activity { get; set; } = activity;
}
