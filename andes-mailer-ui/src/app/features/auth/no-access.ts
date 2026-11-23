import {
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
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
      <!-- Always rendered: screen readers announce text added to a live region, not a new region. -->
      <div role="status">
        @if (switchFailed()) {
          <p #failure class="text-danger-emphasis mb-0" tabindex="-1">
            We couldn't reach Microsoft. Check your connection and try again. If it keeps failing,
            reload this page.
          </p>
        }
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
  private readonly _injector = inject(Injector);
  private readonly _failure = viewChild<ElementRef<HTMLElement>>('failure');

  protected readonly account = computed(() => {
    const user = this._session.user();

    return user?.email ?? user?.name ?? '';
  });

  protected readonly switching = signal(false);

  protected readonly switchFailed = signal(false);

  protected async useDifferentAccount(): Promise<void> {
    this.switching.set(true);

    try {
      await this._session.signIn({ selectAccount: true });
    } catch (cause) {
      console.error(cause);
      this.switching.set(false);
      this.switchFailed.set(true);
      // Disabling the button dropped its focus; the message is where the person continues.
      afterNextRender(() => this._failure()?.nativeElement.focus(), { injector: this._injector });
    }
  }
}
