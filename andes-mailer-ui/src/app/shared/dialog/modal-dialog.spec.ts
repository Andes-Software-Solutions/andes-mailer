import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ModalDialog } from './modal-dialog';

@Component({
  imports: [ModalDialog],
  template: `
    <button id="opener" type="button">Open</button>
    @if (open()) {
      <app-modal-dialog
        [locked]="locked()"
        [busy]="busy()"
        (dismissed)="dismissals = dismissals + 1"
      >
        <div class="modal-body"><button type="button">Inside</button></div>
      </app-modal-dialog>
    }
  `,
})
class Host {
  readonly open = signal(false);
  readonly locked = signal(false);
  readonly busy = signal(false);
  dismissals = 0;
}

describe('ModalDialog', () => {
  let fixture: ComponentFixture<Host>;

  async function show(): Promise<HTMLDialogElement> {
    fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
    fixture.nativeElement.querySelector('#opener').focus();
    fixture.componentInstance.open.set(true);
    await fixture.whenStable();

    return fixture.nativeElement.querySelector('dialog');
  }

  function cancel(dialog: HTMLDialogElement): Event {
    const event = new Event('cancel', { cancelable: true });

    dialog.dispatchEvent(event);

    return event;
  }

  it('opens as soon as it renders', async () => {
    const dialog = await show();

    expect(dialog.open || dialog.hasAttribute('open')).toBe(true);
  });

  it('asks to close on Escape and on a backdrop click, and keeps itself open', async () => {
    const dialog = await show();

    const escape = cancel(dialog);
    dialog.dispatchEvent(new MouseEvent('click', { bubbles: true }));

    expect(escape.defaultPrevented).toBe(true);
    expect(fixture.componentInstance.dismissals).toBe(2);
  });

  it('ignores a click inside its content', async () => {
    const dialog = await show();

    dialog.querySelector<HTMLButtonElement>('.modal-body button')!.click();

    expect(fixture.componentInstance.dismissals).toBe(0);
  });

  it('cannot be dismissed while locked or busy', async () => {
    const dialog = await show();

    fixture.componentInstance.locked.set(true);
    await fixture.whenStable();
    cancel(dialog);
    fixture.componentInstance.locked.set(false);
    fixture.componentInstance.busy.set(true);
    await fixture.whenStable();
    dialog.dispatchEvent(new MouseEvent('click', { bubbles: true }));

    expect(fixture.componentInstance.dismissals).toBe(0);
  });

  // Chromium's close watchers: a second Escape sends a cancel that cannot be refused, then closes.
  function forceClose(dialog: HTMLDialogElement): void {
    dialog.dispatchEvent(new Event('cancel', { cancelable: false }));
    dialog.removeAttribute('open');
    dialog.dispatchEvent(new Event('close'));
  }

  it('opens again when the browser forces a locked dialog closed', async () => {
    const dialog = await show();
    fixture.componentInstance.locked.set(true);
    await fixture.whenStable();

    forceClose(dialog);

    expect(dialog.open || dialog.hasAttribute('open')).toBe(true);
    expect(fixture.componentInstance.dismissals).toBe(0);
  });

  it('asks to close once when the browser forces an unlocked dialog closed', async () => {
    const dialog = await show();

    forceClose(dialog);

    expect(fixture.componentInstance.dismissals).toBe(1);
  });

  it('gives focus back to what opened it', async () => {
    await show();
    fixture.nativeElement.querySelector('.modal-body button').focus();

    fixture.componentInstance.open.set(false);
    await fixture.whenStable();

    expect(document.activeElement?.id).toBe('opener');
  });
});
