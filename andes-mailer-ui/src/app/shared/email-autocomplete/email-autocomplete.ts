import { Component, computed, input, model, output, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormValueControl } from '@angular/forms/signals';
import {
  Observable,
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  of,
  startWith,
  switchMap,
} from 'rxjs';

export interface EmailSuggestion {
  readonly email: string;
  /** What the person recognises the address by, such as a display name. */
  readonly label: string;
}

/** Looks up suggestions for what has been typed so far; errors become the message shown. */
export type EmailSuggester = (query: string) => Observable<readonly EmailSuggestion[]>;

type SuggestState =
  | { readonly status: 'idle' }
  | { readonly status: 'loading' }
  | { readonly status: 'done'; readonly suggestions: readonly EmailSuggestion[] }
  | { readonly status: 'error' };

const MIN_QUERY_LENGTH = 2;
const DEBOUNCE_MS = 300;

let nextId = 0;

/**
 * An email field that suggests addresses while the person types, as an ARIA combobox: arrow keys
 * move through the list, Enter picks, Escape closes. Typing any address is still allowed. Binds
 * with `[formField]`; where the suggestions come from is the caller's `suggest`.
 */
@Component({
  selector: 'app-email-autocomplete',
  styleUrl: './email-autocomplete.scss',
  templateUrl: './email-autocomplete.html',
  // Pointer picks are handled on the host, so the options need no handlers of their own: the
  // keyboard reaches them through the input's aria-activedescendant.
  host: { '(mousedown)': 'onPointer($event)' },
})
export class EmailAutocomplete implements FormValueControl<string> {
  readonly value = model('');
  readonly disabled = input(false);
  readonly readonly = input(false);
  readonly invalid = input(false);
  readonly touched = input(false);
  readonly touch = output<void>();

  readonly suggest = input.required<EmailSuggester>();
  readonly inputId = input.required<string>();
  readonly describedBy = input('');
  readonly placeholder = input('');
  readonly errorMessage = input("We couldn't look up addresses. Type the address instead.");
  /** Marks the field invalid regardless of its validators, for an error the server reported. */
  readonly markInvalid = input(false);

  protected readonly listId = `email-suggestions-${++nextId}`;
  protected readonly open = signal(false);
  protected readonly active = signal(-1);
  private readonly _query = signal('');

  protected readonly state = toSignal(
    toObservable(this._query).pipe(
      debounceTime(DEBOUNCE_MS),
      distinctUntilChanged(),
      switchMap((query) => this.lookup(query)),
    ),
    { initialValue: { status: 'idle' } as SuggestState },
  );

  protected readonly suggestions = computed(() => {
    const state = this.state();

    return state.status === 'done' ? state.suggestions : [];
  });

  protected readonly showInvalid = computed(
    () => this.markInvalid() || (this.invalid() && this.touched()),
  );

  /** A read-only field shows no list, so nothing can pick a suggestion into it. */
  protected readonly expanded = computed(
    () => this.editable() && this.open() && this.state().status !== 'idle',
  );

  private readonly editable = computed(() => !this.disabled() && !this.readonly());

  protected readonly activeId = computed(() =>
    this.expanded() && this.active() >= 0 ? `${this.listId}-${this.active()}` : null,
  );

  protected onInput(event: Event): void {
    const text = (event.target as HTMLInputElement).value;

    this.value.set(text);
    this._query.set(text.trim());
    this.active.set(-1);
    this.open.set(true);
  }

  protected onKeydown(event: KeyboardEvent): void {
    if (!this.editable()) {
      return;
    }

    const count = this.suggestions().length;

    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        this.open.set(true);
        this.active.set(count === 0 ? -1 : (this.active() + 1) % count);
        break;
      case 'ArrowUp':
        event.preventDefault();
        this.open.set(true);
        this.active.set(count === 0 ? -1 : (this.active() - 1 + count) % count);
        break;
      case 'Enter':
        if (this.expanded() && this.active() >= 0) {
          event.preventDefault();
          this.choose(this.suggestions()[this.active()]);
        }
        break;
      case 'Escape':
        // Only while the list shows, so a dialog around the field still closes on a second Escape.
        if (this.expanded()) {
          event.preventDefault();
          event.stopPropagation();
          this.open.set(false);
        }
        break;
    }
  }

  protected onBlur(): void {
    this.open.set(false);
    this.touch.emit();
  }

  protected onPointer(event: MouseEvent): void {
    const option = (event.target as HTMLElement).closest<HTMLElement>('[data-index]');

    if (option && this.editable()) {
      // Keeps focus in the input, so blur does not close the list before the pick lands.
      event.preventDefault();
      this.choose(this.suggestions()[Number(option.dataset['index'])]);
    }
  }

  private choose(suggestion: EmailSuggestion | undefined): void {
    if (!suggestion) {
      return;
    }

    this.value.set(suggestion.email);
    this._query.set('');
    this.active.set(-1);
    this.open.set(false);
  }

  private lookup(query: string): Observable<SuggestState> {
    if (query.length < MIN_QUERY_LENGTH) {
      return of({ status: 'idle' });
    }

    return this.suggest()(query).pipe(
      map((suggestions): SuggestState => ({ status: 'done', suggestions })),
      catchError(() => of<SuggestState>({ status: 'error' })),
      startWith<SuggestState>({ status: 'loading' }),
    );
  }
}
