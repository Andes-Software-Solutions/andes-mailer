import { Component, inject } from '@angular/core';
import { ToastStore } from './toast-store';

/** Where toasts appear: bottom right, full width on phones, announced politely. */
@Component({
  selector: 'app-toast-stack',
  styles: `
    :host {
      position: fixed;
      right: 1.5rem;
      bottom: 1.5rem;
      z-index: 1090;
      display: flex;
      flex-direction: column;
      align-items: flex-end;
      gap: 0.625rem;
      pointer-events: none;
    }

    @media (max-width: 767.98px) {
      :host {
        left: 1rem;
        right: 1rem;
        bottom: 5.5rem;
        align-items: stretch;
      }
    }
  `,
  host: { 'aria-live': 'polite', 'aria-label': 'Notifications' },
  template: `
    @for (toast of store.toasts(); track toast.id) {
      <div class="am-toast" [class.am-toast-error]="toast.tone === 'error'">
        <i
          class="bi"
          [class.bi-check-circle-fill]="toast.tone === 'ok'"
          [class.bi-exclamation-circle-fill]="toast.tone === 'error'"
          aria-hidden="true"
        ></i>
        <span class="am-toast-msg">{{ toast.message }}</span>
        @if (toast.action; as action) {
          <button type="button" class="am-toast-action" (click)="store.act(toast.id)">
            {{ action.label }}
          </button>
        }
        <button
          type="button"
          class="btn btn-ghost btn-icon btn-sm"
          aria-label="Dismiss"
          (click)="store.dismiss(toast.id)"
        >
          <i class="bi bi-x-lg" aria-hidden="true"></i>
        </button>
      </div>
    }
  `,
})
export class ToastStack {
  protected readonly store = inject(ToastStore);
}
