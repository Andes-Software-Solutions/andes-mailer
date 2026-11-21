import { HttpErrorResponse } from '@angular/common/http';
import { InjectionToken, Service } from '@angular/core';
import { TelemetryConfig } from '@core/config/app-config.model';
import type { ApplicationInsights } from '@microsoft/applicationinsights-web';
import { loadApplicationInsights } from './application-insights';
import { apiOperation } from './telemetry-enrichment';

/** The SDK surface the app uses, so specs can hand in a fake. */
export type TelemetryClient = Pick<
  ApplicationInsights,
  | 'loadAppInsights'
  | 'addTelemetryInitializer'
  | 'trackPageView'
  | 'trackEvent'
  | 'trackException'
  | 'setAuthenticatedUserContext'
  | 'clearAuthenticatedUserContext'
  | 'core'
  | 'setTraceCtx'
>;

/** Creates the SDK client; specs replace it with a fake. */
export const TELEMETRY_LOADER = new InjectionToken<
  (config: TelemetryConfig) => Promise<TelemetryClient>
>('TELEMETRY_LOADER', { factory: () => loadApplicationInsights });

/**
 * The one way app code reports to Application Insights. Every call is a no-op until
 * `provideTelemetry` connects the SDK, and stays one when `config.json` has no `telemetry` section.
 * Users are identified by their Entra object id only, never a name or an email address.
 */
@Service()
export class Telemetry {
  private client: TelemetryClient | null = null;

  connect(client: TelemetryClient): void {
    this.client = client;
  }

  /** Records something the user did. Keep `properties` free of personal data. */
  trackEvent(name: string, properties?: Readonly<Record<string, string>>): void {
    this.client?.trackEvent({ name }, properties);
  }

  trackException(error: unknown): void {
    this.client?.trackException({ exception: toError(error) });
  }

  /**
   * Records a screen and starts a new operation for it, so the API calls the screen makes share
   * its operation id instead of the first page's.
   */
  trackPageView(name: string, uri: string, route: string): void {
    const client = this.client;

    if (!client) {
      return;
    }

    // A fresh context, as the SDK's own route tracking does: a context in use is meant to be
    // read-only, and the deprecated setTraceId / setName would also rewrite every parent context.
    const trace = client.core.getTraceCtx(true);

    if (trace) {
      trace.traceId = newTraceId();
      trace.pageName = name;
      client.setTraceCtx(trace);
    }

    client.trackPageView({ name, uri, properties: { route } });
  }

  /** Stored in a cookie, so telemetry sent before the next sign-in check still carries the user. */
  setUser(objectId: string): void {
    this.client?.setAuthenticatedUserContext(objectId, undefined, true);
  }

  clearUser(): void {
    this.client?.clearAuthenticatedUserContext();
  }
}

/**
 * An `HttpErrorResponse` is no `Error`, and its message holds the full URL, query string included,
 * so it is reported by status and operation instead.
 */
function toError(error: unknown): Error {
  if (error instanceof Error) {
    return error;
  }

  if (error instanceof HttpErrorResponse) {
    const failure = new Error(`HTTP ${error.status} ${apiOperation(error.url ?? 'unknown URL')}`);
    failure.name = 'HttpErrorResponse';

    return failure;
  }

  return new Error(
    typeof error === 'string'
      ? error
      : `Non-error thrown: ${Object.prototype.toString.call(error)}`,
  );
}

/** A W3C trace id: 16 random bytes as lowercase hex. */
function newTraceId(): string {
  return Array.from(crypto.getRandomValues(new Uint8Array(16)), (byte) =>
    byte.toString(16).padStart(2, '0'),
  ).join('');
}
