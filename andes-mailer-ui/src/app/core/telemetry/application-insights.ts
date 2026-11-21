import { TelemetryConfig } from '@core/config/app-config.model';
import type { TelemetryClient } from './telemetry';

/**
 * Loads the Application Insights SDK as a chunk of its own, so a deployment without telemetry never
 * downloads it and the initial bundle stays within budget.
 */
export async function loadApplicationInsights({
  connectionString,
}: TelemetryConfig): Promise<TelemetryClient> {
  const { ApplicationInsights, DistributedTracingModes } =
    await import('@microsoft/applicationinsights-web');

  return new ApplicationInsights({
    config: {
      connectionString,
      // provideTelemetry tracks route changes, named after each route's title.
      enableAutoRouteTracking: false,
      // TelemetryErrorHandler reports errors, including the global ones Angular forwards to it.
      disableExceptionTracking: true,
      autoTrackPageVisitTime: true,
      // Only W3C traceparent: the API's OpenTelemetry reads nothing else.
      distributedTracingMode: DistributedTracingModes.W3C,
      enableRequestHeaderTracking: false,
      enableResponseHeaderTracking: false,
    },
  });
}
