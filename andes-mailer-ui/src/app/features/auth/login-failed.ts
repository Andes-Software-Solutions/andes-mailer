import {
  Component,
  ElementRef,
  Injector,
  afterNextRender,
  inject,
  signal,
  viewChild,
} from '@angular/core';
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
  private readonly _injector = inject(Injector);
  private readonly _failure = viewChild<ElementRef<HTMLElement>>('failure');

  /** Latches, so a second press cannot start a second redirect over the first. */
  protected readonly retrying = signal(false);

  protected readonly retryFailed = signal(false);

  protected async retry(): Promise<void> {
    this.retrying.set(true);

    try {
      await this._session.signIn();
    } catch (cause) {
      console.error(cause);
      this.retrying.set(false);
      this.retryFailed.set(true);
      // Disabling the button dropped its focus; the message is where the person continues.
      afterNextRender(() => this._failure()?.nativeElement.focus(), { injector: this._injector });
    }
  }
}
