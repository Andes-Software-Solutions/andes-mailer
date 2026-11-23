import { DOCUMENT } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { SessionStore } from '@core/auth/session-store';
import { SignInLayout } from '@shared/sign-in-layout/sign-in-layout';

/**
 * Covers the app while sign-in runs: on the way to Microsoft, on the way back while the account
 * loads, and when the account could not load.
 */
@Component({
  selector: 'app-sign-in-status',
  imports: [SignInLayout],
  styles: `
    :host {
      position: fixed;
      inset: 0;
      z-index: 1090;
      overflow: auto;
    }

    .status {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 0.875rem;
      padding: 0.5rem 0 0.25rem;
    }

    .status-title {
      margin: 0;
      font-size: 1rem;
      font-weight: 600;
      line-height: 1.5rem;
    }

    .spinner-border {
      width: 1.75rem;
      height: 1.75rem;
      color: var(--am-accent);
    }
  `,
  template: `
    <app-sign-in-layout>
      @switch (session.signInPhase()) {
        @case ('redirecting') {
          <div class="status" role="status" aria-live="polite">
            <div class="bar-indet"></div>
            <p class="status-title">Taking you to Microsoft to sign in…</p>
            <p class="small text-body-secondary mb-0">You'll use your Andes work account</p>
          </div>
          <p class="small text-body-secondary mb-0">
            Not redirected?
            <button
              class="btn btn-link btn-sm p-0 align-baseline"
              type="button"
              (click)="continueToMicrosoft()"
            >
              Continue to Microsoft
            </button>
          </p>
        }
        @case ('error') {
          <div class="alert alert-danger text-start mb-0" role="alert">
            <i class="bi bi-x-circle-fill" aria-hidden="true"></i>
            <div>
              <strong>We couldn't load your account.</strong>
              Check your connection, then try again.
            </div>
          </div>
          <button
            class="btn btn-primary btn-lg w-100"
            type="button"
            [disabled]="retrying()"
            (click)="retry()"
          >
            <i class="bi bi-arrow-repeat" aria-hidden="true"></i>
            Try again
          </button>
        }
        @default {
          <div class="status" role="status" aria-live="polite">
            <div class="spinner-border" aria-hidden="true"></div>
            <p class="status-title">Signing you in…</p>
            <p class="small text-body-secondary mb-0">Getting your teams ready</p>
          </div>
        }
      }
    </app-sign-in-layout>
  `,
})
export class SignInStatus {
  protected readonly session = inject(SessionStore);
  private readonly _router = inject(Router);
  private readonly _document = inject(DOCUMENT);

  protected readonly retrying = signal(false);

  protected async continueToMicrosoft(): Promise<void> {
    try {
      await this.session.signIn();
    } catch {
      // MSAL still holds the stalled redirect's lock, which only a fresh page load releases.
      this._document.location.reload();
    }
  }

  /**
   * Resumes the navigation the failed load cancelled. The router reset the URL when it cancelled,
   * so the target comes from the guard, and `reload` makes a repeat of the current URL run again.
   */
  protected async retry(): Promise<void> {
    const target = this.session.retryUrl() ?? '/';

    this.retrying.set(true);
    await this.session.ensureLoaded();
    this.retrying.set(false);

    if (this.session.status() === 'loaded') {
      await this._router.navigateByUrl(target, {
        onSameUrlNavigation: 'reload',
        replaceUrl: true,
      });
    }
  }
}
