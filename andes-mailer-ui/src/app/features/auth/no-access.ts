import { Component, computed, inject, signal } from '@angular/core';
import { SessionStore } from '@core/auth/session-store';
import { SignInLayout } from '@shared/sign-in-layout/sign-in-layout';

/** For an account Entra signed in that has no active user row; the route's guard sends anyone else home. */
@Component({
  selector: 'app-no-access',
  imports: [SignInLayout],
  template: `
    <app-sign-in-layout>
      <div class="alert alert-warning text-start mb-0" role="alert">
        <i class="bi bi-exclamation-triangle-fill" aria-hidden="true"></i>
        <div>
          <strong>This account can't use Andes Mailer yet.</strong>
          @if (account()) {
            You're signed in as {{ account() }}.
          }
          Ask IT to give you access, or sign in with a different account.
        </div>
      </div>
      <button
        class="btn btn-outline btn-lg w-100"
        type="button"
        [disabled]="switching()"
        (click)="useDifferentAccount()"
      >
        <i class="bi bi-person" aria-hidden="true"></i>
        Use a different account
      </button>
    </app-sign-in-layout>
  `,
})
export class NoAccess {
  private readonly _session = inject(SessionStore);

  protected readonly account = computed(() => {
    const user = this._session.user();

    return user?.email ?? user?.name ?? '';
  });

  protected readonly switching = signal(false);

  protected async useDifferentAccount(): Promise<void> {
    this.switching.set(true);

    try {
      await this._session.signIn({ selectAccount: true });
    } catch (cause) {
      console.error(cause);
      this.switching.set(false);
    }
  }
}
