import { DOCUMENT } from '@angular/common';
import {
  DestroyRef,
  EnvironmentProviders,
  Service,
  inject,
  provideAppInitializer,
} from '@angular/core';
import { InteractionRequiredAuthError } from '@azure/msal-browser';
import { Auth, TokenOptions } from './auth';

/**
 * Ahead of MSAL's own 5-minute renewal window, so a request always finds a valid cached token,
 * with room for a 10 s hidden-iframe attempt and a retry.
 */
const RENEWAL_LEAD_MS = 10 * 60_000;
const RETRY_DELAY_MS = 60_000;
/** A failure that persists, such as an iframe that never completes, would otherwise call Entra every minute in every tab. */
const MAX_RETRY_DELAY_MS = 15 * 60_000;

/**
 * Renews the access token before it expires, so a session left open for hours never fails a
 * request. Hidden tabs throttle timers and a sleeping PC pauses them, so returning to the tab or
 * coming back online renews at once.
 */
@Service()
export class TokenRenewal {
  private readonly _auth = inject(Auth);
  private readonly _document = inject(DOCUMENT);
  private _timer: ReturnType<typeof setTimeout> | undefined;
  private _started = false;
  private _stopped = false;
  private _failures = 0;

  private readonly _onVisibilityChange = (): void => {
    if (this._document.visibilityState === 'visible') {
      void this.renew();
    }
  };

  // Silent, because the connection can come back while the tab is hidden or mid-task.
  private readonly _onOnline = (): void => {
    void this.renew({ interactive: false });
  };

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  async start(): Promise<void> {
    if (this._started) {
      return;
    }

    this._started = true;

    const { account } = await this._auth.ready();

    if (!account || this._stopped) {
      return;
    }

    this._document.addEventListener('visibilitychange', this._onVisibilityChange);
    this._document.defaultView?.addEventListener('online', this._onOnline);

    // A sign-in Entra needs now is left to the page's first request, which starts it anyway.
    await this.renew({ interactive: false });
  }

  /**
   * Interactive by default: when the user returns to the tab is the moment to redirect, before
   * they start work the redirect would lose.
   */
  private async renew(options: TokenOptions = {}): Promise<void> {
    try {
      const { expiresOn } = await this._auth.acquireToken(options);

      this._failures = 0;

      if (expiresOn) {
        const lifetime = expiresOn.getTime() - Date.now();
        // At most half the lifetime, so a short-lived token isn't refreshed on every attempt.
        const lead = Math.min(RENEWAL_LEAD_MS, lifetime / 2);

        this.schedule(Math.max(lifetime - lead, RETRY_DELAY_MS));
      }
    } catch (cause) {
      // Either the redirect has started, or the next return to the tab or request will start it.
      if (cause instanceof InteractionRequiredAuthError) {
        return;
      }

      console.error(cause);
      this.schedule(Math.min(RETRY_DELAY_MS * 2 ** this._failures, MAX_RETRY_DELAY_MS));
      this._failures += 1;
    }
  }

  private schedule(delayMs: number): void {
    clearTimeout(this._timer);

    if (this._stopped) {
      return;
    }

    this._timer = setTimeout(() => void this.renew({ refresh: true, interactive: false }), delayMs);
  }

  private stop(): void {
    this._stopped = true;
    clearTimeout(this._timer);
    this._document.removeEventListener('visibilitychange', this._onVisibilityChange);
    this._document.defaultView?.removeEventListener('online', this._onOnline);
  }
}

export function provideTokenRenewal(): EnvironmentProviders {
  return provideAppInitializer(() => {
    // Not awaited: renewal must never hold up bootstrap.
    void inject(TokenRenewal).start();
  });
}
