import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FormField, form, readonly } from '@angular/forms/signals';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { EmailAutocomplete, EmailSuggester } from './email-autocomplete';

const suggestions = [
  { email: 'press@example.com', label: 'Press Office' },
  { email: 'presales@example.com', label: 'Presales' },
];

@Component({
  imports: [EmailAutocomplete, FormField],
  template: `
    <app-email-autocomplete inputId="mailbox" [suggest]="suggest()" [formField]="form.mailbox" />
  `,
})
class Host {
  readonly frozen = signal(false);
  readonly model = signal({ mailbox: '' });
  readonly form = form(this.model, (path) => readonly(path.mailbox, () => this.frozen()));
  readonly lookups = vi.fn<EmailSuggester>(() => of(suggestions));
  readonly suggest = signal<EmailSuggester>(this.lookups);
}

describe('EmailAutocomplete', () => {
  let fixture: ComponentFixture<Host>;

  beforeEach(async () => {
    fixture = TestBed.createComponent(Host);
    await fixture.whenStable();
  });

  function field(): HTMLInputElement {
    return fixture.nativeElement.querySelector('#mailbox');
  }

  function options(): HTMLElement[] {
    return [...fixture.nativeElement.querySelectorAll('[role="option"]')];
  }

  async function type(text: string): Promise<void> {
    field().value = text;
    field().dispatchEvent(new Event('input'));
    // Past the 300 ms debounce, in real time: fake timers would also freeze Angular's scheduler.
    await new Promise((resolve) => setTimeout(resolve, 350));
    await fixture.whenStable();
  }

  async function press(key: string): Promise<void> {
    field().dispatchEvent(new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true }));
    await fixture.whenStable();
  }

  it('suggests addresses once two characters are typed', async () => {
    await type('p');
    expect(fixture.componentInstance.lookups).not.toHaveBeenCalled();

    await type('pre');

    expect(fixture.componentInstance.lookups).toHaveBeenCalledWith('pre');
    expect(options().map((option) => option.textContent)).toEqual([
      expect.stringContaining('Press Office'),
      expect.stringContaining('Presales'),
    ]);
    expect(field().getAttribute('aria-expanded')).toBe('true');
  });

  it('keeps what is typed in the field while searching', async () => {
    await type('pre');

    expect(fixture.componentInstance.model().mailbox).toBe('pre');
  });

  it('picks a suggestion with the arrow keys and Enter', async () => {
    await type('pre');

    await press('ArrowDown');
    await press('ArrowDown');
    expect(field().getAttribute('aria-activedescendant')).toBe(options()[1].id);
    await press('Enter');

    expect(fixture.componentInstance.model().mailbox).toBe('presales@example.com');
    expect(field().getAttribute('aria-expanded')).toBe('false');
  });

  it('picks a suggestion with the pointer', async () => {
    await type('pre');

    options()[0].dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    await fixture.whenStable();

    expect(fixture.componentInstance.model().mailbox).toBe('press@example.com');
  });

  it('closes on Escape without letting the key reach a dialog around it', async () => {
    await type('pre');
    const escape = new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true });

    field().dispatchEvent(escape);
    await fixture.whenStable();

    expect(escape.defaultPrevented).toBe(true);
    expect(field().getAttribute('aria-expanded')).toBe('false');
  });

  it('picks nothing while the field is read-only, as during a save', async () => {
    await type('pre');
    const option = options()[0];
    fixture.componentInstance.frozen.set(true);
    await fixture.whenStable();

    await press('ArrowDown');
    await press('Enter');
    option.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
    await fixture.whenStable();

    expect(fixture.componentInstance.model().mailbox).toBe('pre');
    expect(field().getAttribute('aria-expanded')).toBe('false');
  });

  it('says so when the lookup fails, and still accepts a typed address', async () => {
    fixture.componentInstance.suggest.set(() => throwError(() => new Error('429')));
    await fixture.whenStable();

    await type('team@example.com');

    expect(fixture.nativeElement.textContent).toContain("We couldn't look up addresses");
    expect(fixture.componentInstance.model().mailbox).toBe('team@example.com');
  });
});
