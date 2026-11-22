using System.Diagnostics;

using Andes.Mailer.Common.Constants;

namespace Andes.Mailer.Service.Observability;

/// <summary>
/// Holds the activity source the background services start their activities from, so the work of one dispatch run or one upload, with
/// its store and Graph calls and its logs, shares one trace.
/// </summary>
internal static class AndesMailerActivitySource
{
    /// <summary>
    /// Gets the activity source named <see cref="TelemetryNames.ActivitySourceName"/>.
    /// </summary>
    public static ActivitySource Instance { get; } = new(TelemetryNames.ActivitySourceName);
}
