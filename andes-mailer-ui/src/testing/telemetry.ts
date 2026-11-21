import { Provider } from '@angular/core';
import type { ITelemetryItem } from '@microsoft/applicationinsights-web';
import { vi } from 'vitest';
import { TELEMETRY_LOADER, TelemetryClient } from '../app/core/telemetry/telemetry';

type TelemetryInitializer = (item: ITelemetryItem) => boolean | void;

/** A fake SDK that records every call, plus the initializers and trace context the app touched. */
export interface FakeTelemetryClient extends TelemetryClient {
  readonly initializers: TelemetryInitializer[];
  /** The context the last page view installed, or `null` before the first one. */
  readonly installedTrace: () => { readonly traceId: string; readonly pageName: string } | null;
}

export function fakeTelemetryClient(): FakeTelemetryClient {
  const initializers: TelemetryInitializer[] = [];
  let installed: { traceId: string; pageName: string } | null = null;

  // Through unknown: the fake core and trace contexts implement only what the app touches.
  return {
    initializers,
    installedTrace: () => installed,
    loadAppInsights: vi.fn(),
    addTelemetryInitializer: vi.fn((initializer: TelemetryInitializer) => {
      initializers.push(initializer);

      return { remove: () => undefined };
    }),
    trackPageView: vi.fn(),
    trackEvent: vi.fn(),
    trackException: vi.fn(),
    setAuthenticatedUserContext: vi.fn(),
    clearAuthenticatedUserContext: vi.fn(),
    core: { getTraceCtx: vi.fn(() => ({ traceId: '', pageName: '' })) },
    setTraceCtx: vi.fn((trace: { traceId: string; pageName: string }) => {
      installed = trace;
    }),
  } as unknown as FakeTelemetryClient;
}

/** Hands `provideTelemetry` the fake instead of loading the SDK. */
export function provideFakeTelemetryClient(load: () => Promise<TelemetryClient>): Provider {
  return { provide: TELEMETRY_LOADER, useValue: load };
}
