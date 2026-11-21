using System.Collections.Concurrent;
using System.Diagnostics;

using Andes.Mailer.Common.Constants;

namespace Andes.Mailer.Unit.Test.TestInfrastructure;

/// <summary>
/// Records the activities the background services stop, as an OpenTelemetry tracer would see them. The listener is process-wide, so a
/// test picks its own activities out by tag rather than assuming it is the only producer.
/// </summary>
internal sealed class ActivityCollector : IDisposable
{
    private readonly ConcurrentQueue<Activity> _stopped = new();
    private readonly ActivityListener _listener;

    public ActivityCollector()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TelemetryNames.ActivitySourceName,
            Sample = static (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = _stopped.Enqueue,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    public async Task<Activity> WaitForAsync(Func<Activity, bool> match)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var found = _stopped.FirstOrDefault(match);
            if (found is not null)
            {
                return found;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25), TestContext.Current.CancellationToken);
        }

        throw new TimeoutException("No matching activity stopped.");
    }
}
