import { Component, inject, signal } from '@angular/core';
import { SessionStore } from '@core/auth/session-store';
import { SignInLayout } from '@shared/sign-in-layout/sign-in-layout';

/** Unguarded: a guard would send a user whose sign-in just failed straight back to Entra. */
@Component({
  selector: 'app-login-failed',
  imports: [SignInLayout],
  templateUrl: './login-failed.html',
})
export class LoginFailed {
  private readonly _session = inject(SessionStore);

  /** Latches, so a second press cannot start a second redirect over the first. */
  protected readonly retrying = signal(false);

  /** MSAL's redirect lock has no release API, so after this only a reload can try again. */
  protected readonly retryFailed = signal(false);

  protected async retry(): Promise<void> {
    this.retrying.set(true);

    try {
      await this._session.signIn();
    } catch (cause) {
      console.error(cause);
      this.retrying.set(false);
      this.retryFailed.set(true);
    }
  }
}
