import { DOCUMENT, Location } from '@angular/common';
import { Service, inject } from '@angular/core';
import { AccountInfo, InteractionRequiredAuthError } from '@azure/msal-browser';
import { APP_CONFIG } from '@core/config/app-config-token';
import { MSAL_INSTANCE } from './msal-instance-token';

/** Set for the trip to Entra, so a return without an account fails instead of redirecting again. */
const SIGN_IN_ATTEMPT_KEY = 'andes-mailer.signInAttempt';

/** The outcome of resolving a sign-in redirect. */
export interface AuthHandshake {
  readonly account: AccountInfo | null;
  /** Set when the redirect came back carrying a failure. */
  readonly failed: boolean;
}

@Service()
export class Auth {
  private readonly _instance = inject(MSAL_INSTANCE);
  private readonly _scopes = [...inject(APP_CONFIG).auth.apiScopes];
  private readonly _location = inject(Location);
  private readonly _document = inject(DOCUMENT);
  private _handshake: Promise<AuthHandshake> | null = null;

  /** Whether a sign-in left for Entra and has not come back with an account. */
  get signInAttempted(): boolean {
    return sessionStorage.getItem(SIGN_IN_ATTEMPT_KEY) !== null;
  }

  /**
   * Resolves any sign-in redirect and selects the account, once per page load. It must run on
   * every load, because it is also what releases the lock of an abandoned redirect.
   */
  ready(): Promise<AuthHandshake> {
    return (this._handshake ??= this.handshake());
  }

  /**
   * @param url The in-app URL to land on after signing in.
   * @throws When the redirect cannot start, such as when Entra is unreachable.
   */
  async signIn(url: string): Promise<void> {
    await this.ready();

    sessionStorage.setItem(SIGN_IN_ATTEMPT_KEY, 'true');

    await this._instance.loginRedirect({
      scopes: this._scopes,
      redirectStartPage: new URL(this._location.prepareExternalUrl(url), this._document.baseURI)
        .href,
    });
  }

  /** @throws When no token is available, including while a redirect to Entra starts. */
  async getAccessToken(): Promise<string> {
    const { account } = await this.ready();

    if (!account) {
      throw new Error('No account is signed in.');
    }

    const request = { scopes: this._scopes, account };

    try {
      const { accessToken } = await this._instance.acquireTokenSilent(request);

      return accessToken;
    } catch (cause) {
      if (cause instanceof InteractionRequiredAuthError) {
        await this._instance.acquireTokenRedirect(request);
      }

      throw cause;
    }
  }

  private async handshake(): Promise<AuthHandshake> {
    try {
      const result = await this._instance.handleRedirectPromise();
      const account = result?.account ?? this._instance.getAllAccounts()[0] ?? null;

      if (account) {
        sessionStorage.removeItem(SIGN_IN_ATTEMPT_KEY);
      }

      return { account, failed: false };
    } catch (cause) {
      console.error(cause);

      return { account: null, failed: true };
    }
  }
}
