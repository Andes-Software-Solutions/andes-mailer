import { mergeApplicationConfig } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { createStandardPublicClientApplication } from '@azure/msal-browser';
import { MSAL_INSTANCE, buildMsalConfig } from '@core/auth/msal-instance-token';
import { completeRedirect, isRedirectUrl } from '@core/auth/redirect-bridge';
import { APP_CONFIG } from '@core/config/app-config-token';
import { loadAppConfig } from '@core/config/load-app-config';
import { appConfig } from './app/app.config';
import { App } from './app/app';

async function start(): Promise<void> {
  const baseUri = document.baseURI;

  if (isRedirectUrl(location.href, baseUri)) {
    await completeRedirect(location, baseUri);

    return;
  }

  const config = await loadAppConfig(baseUri);

  // Before bootstrap, because no DI factory can await MSAL's initialization.
  const msal = await createStandardPublicClientApplication(buildMsalConfig(config, baseUri));

  await bootstrapApplication(
    App,
    mergeApplicationConfig(appConfig, {
      providers: [
        { provide: APP_CONFIG, useValue: config },
        { provide: MSAL_INSTANCE, useValue: msal },
      ],
    }),
  );
}

start().catch((err: unknown) => {
  console.error(err);
  document.body.textContent = 'Andes Mailer could not start. Reload the page to try again.';
});
