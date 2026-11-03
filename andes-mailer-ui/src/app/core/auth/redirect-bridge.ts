import { broadcastResponseToMainFrame } from '@azure/msal-browser/redirect-bridge';
import { routePaths } from '@core/navigation/route-paths';

/** Registered on the Entra app registration, which matches it verbatim. */
const REDIRECT_PATH = 'auth';

export function redirectUri(baseUri: string): string {
  return new URL(REDIRECT_PATH, baseUri).href;
}

export function isRedirectUrl(url: string, baseUri: string): boolean {
  return new URL(url).pathname === new URL(redirectUri(baseUri)).pathname;
}

/**
 * Runs before Angular boots rather than in a routed component: the router could rewrite the hash
 * the bridge reads, and the hidden iframe of a silent token renewal has no use for the app. For a
 * redirect the bridge returns to the page that started the sign-in; for an iframe it posts the
 * response to the waiting main frame.
 */
export async function completeRedirect(
  location: Pick<Location, 'replace'>,
  baseUri: string,
  broadcast: () => Promise<void> = broadcastResponseToMainFrame,
): Promise<void> {
  try {
    await broadcast();
  } catch (cause) {
    // Throws when `/auth` carries no response, such as when it is opened directly.
    console.error(cause);
    location.replace(new URL(routePaths.loginFailed, baseUri).href);
  }
}
