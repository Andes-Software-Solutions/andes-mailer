import { ErrorHandler, Injectable, inject } from '@angular/core';
import { Telemetry } from './telemetry';

/**
 * Angular's default handler plus a report to Application Insights. `provideBrowserGlobalErrorListeners`
 * forwards uncaught errors and unhandled rejections here, so the SDK's own window hooks stay off.
 */
@Injectable()
export class TelemetryErrorHandler implements ErrorHandler {
  private readonly telemetry = inject(Telemetry);

  handleError(error: unknown): void {
    this.telemetry.trackException(error);
    console.error('ERROR', error);
  }
}
