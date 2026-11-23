import { Component } from '@angular/core';
import { AppLogo } from '@shared/brand/app-logo';

/**
 * The centred card every sign-in state sits in: redirecting, signing in, failed, signed out, and
 * no access. The state goes in as content.
 */
@Component({
  selector: 'app-sign-in-layout',
  imports: [AppLogo],
  styleUrl: './sign-in-layout.scss',
  template: `
    <svg
      class="hills"
      viewBox="0 0 1440 260"
      preserveAspectRatio="none"
      aria-hidden="true"
      focusable="false"
    >
      <path
        class="hills-back"
        d="M0 200 L180 150 L300 185 L470 92 L620 170 L760 122 L900 190 L1060 72 L1220 160 L1340 122 L1440 150 V260 H0 Z"
      />
      <polyline
        class="hills-line"
        points="0,200 180,150 300,185 470,92 620,170 760,122 900,190 1060,72 1220,160 1340,122 1440,150"
        vector-effect="non-scaling-stroke"
      />
      <path
        class="hills-front"
        d="M0 232 L240 192 L420 226 L640 172 L820 216 L1000 182 L1200 222 L1440 192 V260 H0 Z"
      />
    </svg>

    <main class="main">
      <div class="card sign-in-card">
        <app-logo class="logo" [height]="52" product="" priority />
        <div>
          <h1 class="mb-2">Andes Mailer</h1>
          <p class="tagline mb-0">Create and send company emails through Outlook</p>
        </div>
        <ng-content />
      </div>
    </main>

    <footer class="footer">© 2026 Andes Software Solutions LLC</footer>
  `,
})
export class SignInLayout {}
