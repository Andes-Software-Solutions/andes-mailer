import { mergeApplicationConfig } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { completeRedirect, isRedirectUrl } from '@core/auth/redirect-bridge';
import { APP_CONFIG } from '@core/config/app-config-token';
import { loadAppConfig } from '@core/config/load-app-config';
import { applyInitialTheme } from '@core/theme/theme-preference';
import { appConfig } from './app/app.config';
import { App } from './app/app';

async function start(): Promise<void> {
  const baseUri = document.baseURI;

  if (isRedirectUrl(location.href, baseUri)) {
    await completeRedirect(location, baseUri);

    return;
  }

  applyInitialTheme(document);

  const config = await loadAppConfig(baseUri);

  await bootstrapApplication(
    App,
    mergeApplicationConfig(appConfig, {
      providers: [{ provide: APP_CONFIG, useValue: config }],
    }),
  );
}

start().catch((err: unknown) => {
  console.error(err);
  document.body.textContent = 'Andes Mailer could not start. Reload the page to try again.';
});
