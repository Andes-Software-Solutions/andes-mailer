import { Component, booleanAttribute, computed, input, output } from '@angular/core';
import { ModalDialog } from './modal-dialog';

export type ConfirmTone = 'danger' | 'ok' | 'warn' | 'info';

const TONE_ICONS: Readonly<Record<ConfirmTone, string>> = {
  danger: 'trash3',
  ok: 'shield-check',
  warn: 'exclamation-triangle',
  info: 'info-circle',
};

let nextId = 0;

/**
 * A short question with one way forward: says exactly what will happen and names the action on its
 * button. Without `cancelLabel` it only informs, with a single button that closes it. Project the
 * body text; it is the dialog's description.
 */
@Component({
  selector: 'app-confirm-dialog',
  imports: [ModalDialog],
  template: `
    <app-modal-dialog
      size="sm"
      sheet
      [labelledBy]="titleId"
      [describedBy]="bodyId"
      [busy]="busy()"
      [locked]="!cancelLabel()"
      (dismissed)="cancelled.emit()"
    >
      <div class="modal-header border-0 align-items-start gap-3">
        <span class="modal-ic" [class]="'modal-ic-' + tone()" aria-hidden="true">
          <i class="bi" [class]="'bi-' + icon()"></i>
        </span>
        <h2 class="modal-title flex-grow-1" [id]="titleId">{{ heading() }}</h2>
        @if (cancelLabel()) {
          <button
            type="button"
            class="btn btn-ghost btn-icon btn-sm"
            aria-label="Close"
            [disabled]="busy()"
            (click)="cancelled.emit()"
          >
            <i class="bi bi-x-lg" aria-hidden="true"></i>
          </button>
        }
      </div>
      <div class="modal-body pt-0" [id]="bodyId">
        <ng-content />
      </div>
      <div class="modal-footer">
        @if (cancelLabel()) {
          <button
            type="button"
            class="btn btn-outline"
            [disabled]="busy()"
            (click)="cancelled.emit()"
          >
            {{ cancelLabel() }}
          </button>
        }
        <button
          type="button"
          class="btn"
          [class.btn-danger]="tone() === 'danger'"
          [class.btn-primary]="tone() !== 'danger'"
          [disabled]="busy()"
          [attr.aria-busy]="busy() || null"
          (click)="confirmed.emit()"
        >
          @if (busy()) {
            <span class="spinner-border spinner-border-sm" aria-hidden="true"></span>
          }
          {{ confirmLabel() }}
        </button>
      </div>
    </app-modal-dialog>
  `,
})
export class ConfirmDialog {
  protected readonly titleId = `confirm-title-${++nextId}`;
  protected readonly bodyId = `confirm-body-${nextId}`;

  readonly heading = input.required<string>();
  readonly confirmLabel = input.required<string>();
  /** Leave empty for a dialog that only informs. */
  readonly cancelLabel = input('Cancel');
  readonly tone = input<ConfirmTone>('ok');
  readonly busy = input(false, { transform: booleanAttribute });

  readonly confirmed = output<void>();
  readonly cancelled = output<void>();

  protected readonly icon = computed(() => TONE_ICONS[this.tone()]);
}
