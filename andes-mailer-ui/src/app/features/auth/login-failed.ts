import { DOCUMENT, Location } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { MsalService } from '@azure/msal-angular';
import { APP_CONFIG } from '@core/config/app-config-token';
import { routePaths } from '@core/navigation/route-paths';
import { firstValueFrom } from 'rxjs';

/** Unguarded: a guard would send a user whose sign-in just failed straight back to Entra. */
@Component({
  selector: 'app-login-failed',
  templateUrl: './login-failed.html',
})
export class LoginFailed {
  private readonly _msal = inject(MsalService);
  private readonly _scopes = [...inject(APP_CONFIG).auth.apiScopes];
  private readonly _location = inject(Location);
  private readonly _document = inject(DOCUMENT);

  /** Latches, so a second press cannot start a second redirect over the first. */
  protected readonly retrying = signal(false);

  /** MSAL's redirect lock has no release API, so after this only a reload can try again. */
  protected readonly retryFailed = signal(false);

  protected async retry(): Promise<void> {
    this.retrying.set(true);

    try {
      await firstValueFrom(
        this._msal.loginRedirect({
          scopes: this._scopes,
          // The home page, not this one: MSAL returns to the page a sign-in names as its start.
          redirectStartPage: new URL(
            this._location.prepareExternalUrl(routePaths.home),
            this._document.baseURI,
          ).href,
        }),
      );
    } catch (cause) {
      console.error(cause);
      this.retrying.set(false);
      this.retryFailed.set(true);
    }
  }
}
