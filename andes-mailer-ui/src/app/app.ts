import { Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SessionStore } from '@core/auth/session-store';
import { SignInStatus } from '@core/layout/sign-in-status';
import { ThemeStore } from '@core/theme/theme-store';

@Component({
  imports: [RouterOutlet, SignInStatus],
  selector: 'app-root',
  styleUrl: './app.scss',
  templateUrl: './app.html',
})
export class App {
  protected readonly session = inject(SessionStore);

  constructor() {
    // Created here so the theme follows the operating system from the first page on.
    inject(ThemeStore);
  }
}
