import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { idempotencyInterceptor } from '@core/api/idempotency/idempotency-interceptor';
import { authInterceptor } from '@core/auth/auth-interceptor';
import { provideMsal } from '@core/auth/provide-msal';
import { provideTelemetry } from '@core/telemetry/provide-telemetry';
import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    // Idempotency first, so each retry it makes passes through the auth interceptor for a fresh token.
    provideHttpClient(withInterceptors([idempotencyInterceptor, authInterceptor])),
    ...provideMsal(),
    ...provideTelemetry(),
  ],
};
