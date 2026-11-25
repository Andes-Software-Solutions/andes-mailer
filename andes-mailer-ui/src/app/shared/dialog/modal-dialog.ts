import {
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  booleanAttribute,
  inject,
  input,
  output,
  viewChild,
} from '@angular/core';

export type ModalSize = 'sm' | 'md' | 'lg';

function show(dialog: HTMLDialogElement): void {
  // jsdom has no showModal; the dialog still renders there, as an open non-modal one.
  if (typeof dialog.showModal === 'function') {
    dialog.showModal();
  } else {
    dialog.setAttribute('open', '');
  }
}

/**
 * A modal on the native `<dialog>`: the browser traps focus, makes the page inert, and closes on
 * Escape. It opens when it renders, so the parent shows it with `@if` and removes it to close;
 * `dismissed` asks the parent to do that after Escape, a backdrop click, or a close button.
 *
 * Project a `.modal-header`, `.modal-body`, and `.modal-footer`, as with Bootstrap's modal markup.
 * On phones it fills the screen, or rises from the bottom with `sheet`.
 */
@Component({
  selector: 'app-modal-dialog',
  styleUrl: './modal-dialog.scss',
  template: `
    <dialog
      #dialog
      class="am-dialog"
      [class]="'am-dialog-' + size()"
      [class.am-dialog-sheet]="sheet()"
      [attr.aria-labelledby]="labelledBy() || null"
      [attr.aria-describedby]="describedBy() || null"
      [attr.aria-busy]="busy() || null"
      (cancel)="onCancel($event)"
    >
      <div class="modal-content">
        <ng-content />
      </div>
    </dialog>
  `,
})
export class ModalDialog {
  private readonly _dialog = viewChild.required<ElementRef<HTMLDialogElement>>('dialog');

  readonly size = input<ModalSize>('md');
  /** A bottom sheet rather than a full screen on phones; for short confirmations. */
  readonly sheet = input(false, { transform: booleanAttribute });
  /** The id of the element that names the dialog, usually its title. */
  readonly labelledBy = input('');
  readonly describedBy = input('');
  /** Ignores Escape and backdrop clicks, for dialogs that must be answered or are saving. */
  readonly locked = input(false, { transform: booleanAttribute });
  readonly busy = input(false, { transform: booleanAttribute });

  readonly dismissed = output<void>();

  constructor() {
    const destroyRef = inject(DestroyRef);

    afterNextRender(() => {
      const dialog = this._dialog().nativeElement;
      const opener = dialog.ownerDocument.activeElement;
      // Pointer only: Escape (the cancel event) is the keyboard way to dismiss.
      const onBackdrop = (event: MouseEvent): void => this.onBackdrop(event);
      const onClose = (): void => this.onClose(dialog);

      dialog.addEventListener('click', onBackdrop);
      dialog.addEventListener('close', onClose);
      show(dialog);

      // Removing an open dialog skips the browser's own focus return, so it is done here.
      destroyRef.onDestroy(() => {
        dialog.removeEventListener('click', onBackdrop);
        dialog.removeEventListener('close', onClose);

        if (dialog.open && typeof dialog.close === 'function') {
          dialog.close();
        }

        if (opener instanceof HTMLElement && opener.isConnected) {
          opener.focus();
        }
      });
    });
  }

  protected onCancel(event: Event): void {
    // Chromium lets a page refuse only one close request per user activation, and Escape is not
    // one, so a second Escape or back gesture sends a cancel that cannot be refused. The dialog
    // then closes, and onClose answers that.
    if (!event.cancelable) {
      return;
    }

    event.preventDefault();

    if (!this.locked() && !this.busy()) {
      this.dismissed.emit();
    }
  }

  // Only a close the browser forced reaches this: teardown removes the listener before closing.
  private onClose(dialog: HTMLDialogElement): void {
    if (this.locked() || this.busy()) {
      show(dialog);
    } else {
      this.dismissed.emit();
    }
  }

  // A click on the <dialog> itself, outside its content box, lands on the backdrop.
  private onBackdrop(event: MouseEvent): void {
    if (event.target === this._dialog().nativeElement && !this.locked() && !this.busy()) {
      this.dismissed.emit();
    }
  }
}
