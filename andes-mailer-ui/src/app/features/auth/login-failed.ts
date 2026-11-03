import { Component, inject, signal } from '@angular/core';
import { Auth } from '@core/auth/auth';
import { routePaths } from '@core/navigation/route-paths';

/** Unguarded: a guard would send a user whose sign-in just failed straight back to Entra. */
@Component({
  selector: 'app-login-failed',
  templateUrl: './login-failed.html',
})
export class LoginFailed {
  private readonly _auth = inject(Auth);

  /** Latches, so a second press cannot start a second redirect over the first. */
  protected readonly retrying = signal(false);

  /** MSAL's redirect lock has no release API, so after this only a reload can try again. */
  protected readonly retryFailed = signal(false);

  protected async retry(): Promise<void> {
    this.retrying.set(true);

    try {
      // The home page, not this one: MSAL returns to the page a sign-in names as its start.
      await this._auth.signIn(routePaths.home);
    } catch (cause) {
      console.error(cause);
      this.retrying.set(false);
      this.retryFailed.set(true);
    }
  }
}
