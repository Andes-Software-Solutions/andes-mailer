import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormField, disabled, form, readonly } from '@angular/forms/signals';
import { RichTextEditor } from './rich-text-editor';

@Component({
  imports: [FormField, RichTextEditor],
  template: `<app-rich-text-editor label="Signature" [formField]="form.signature" />`,
})
class Host {
  readonly locked = signal(false);
  readonly frozen = signal(false);
  readonly model = signal({ signature: '<p><strong>Sales</strong></p>' });
  readonly form = form(this.model, (path) => {
    disabled(path.signature, () => this.locked());
    readonly(path.signature, () => this.frozen());
  });
}

describe('RichTextEditor', () => {
  let fixture: ComponentFixture<Host>;

  async function render(): Promise<HTMLElement> {
    fixture = TestBed.createComponent(Host);
    await fixture.whenStable();

    return fixture.nativeElement as HTMLElement;
  }

  function doc(element: HTMLElement): HTMLElement {
    return element.querySelector<HTMLElement>('.rte-doc')!;
  }

  it('shows the field value as a labelled editable document', async () => {
    const element = await render();

    expect(doc(element).innerHTML).toContain('<strong>Sales</strong>');
    expect(doc(element).getAttribute('aria-label')).toBe('Signature');
    expect(doc(element).getAttribute('contenteditable')).toBe('true');
  });

  it('replaces the document when the value changes from outside, as Discard does', async () => {
    const element = await render();

    fixture.componentInstance.model.set({ signature: '<p>Support</p>' });
    await fixture.whenStable();

    expect(doc(element).textContent).toBe('Support');
  });

  it('writes what is typed back to the field', async () => {
    const element = await render();
    const paragraph = doc(element).querySelector('p')!;

    paragraph.textContent = 'Sales team';
    await new Promise((resolve) => setTimeout(resolve, 0));
    await fixture.whenStable();

    expect(fixture.componentInstance.model().signature).toContain('Sales team');
  });

  it('stops editing and formatting while the field is disabled', async () => {
    const element = await render();

    fixture.componentInstance.locked.set(true);
    await fixture.whenStable();

    expect(doc(element).getAttribute('contenteditable')).toBe('false');
    expect(element.querySelector<HTMLButtonElement>('button[aria-label="Bold"]')!.disabled).toBe(
      true,
    );
  });

  it('keeps an open link box from changing the value while the field is read-only', async () => {
    const element = await render();
    element.querySelector<HTMLButtonElement>('button[aria-label="Insert link"]')!.click();
    await fixture.whenStable();

    fixture.componentInstance.frozen.set(true);
    await fixture.whenStable();

    const apply = [...element.querySelectorAll<HTMLButtonElement>('.rte-link button')].find(
      (button) => button.textContent?.trim() === 'Apply',
    );
    expect(apply?.disabled).toBe(true);
  });
});
