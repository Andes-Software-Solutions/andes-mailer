import {
  Component,
  ElementRef,
  afterNextRender,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { DIRECTORY_QUERY_MIN_LENGTH, DirectoryApi } from '@core/api/directory-api';
import { DirectoryUser } from '@core/api/directory-user.model';
import { problemMessage, problemStatus } from '@core/api/problem-message';
import { TeamRole } from '@core/api/team-member.model';
import { ModalDialog } from '@shared/dialog/modal-dialog';
import { initials } from '@shared/formatting/initials';
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
import { AddFailure } from './team-members-store';

export interface AddMembersRequest {
  readonly people: readonly DirectoryUser[];
  readonly role: TeamRole;
}

type SearchState =
  | { readonly status: 'idle' }
  | { readonly status: 'loading' }
  | { readonly status: 'done'; readonly results: readonly DirectoryUser[] }
  | { readonly status: 'error'; readonly message: string };

const SEARCH_DEBOUNCE_MS = 300;

/**
 * Picks people from the directory and adds them in one go, all with the same role. People already on the team stay visible but cannot be picked.
 */
@Component({
  selector: 'app-add-members',
  imports: [ModalDialog],
  styleUrl: './add-members.scss',
  templateUrl: './add-members.html',
})
export class AddMembers {
  private readonly _directory = inject(DirectoryApi);
  private readonly _searchInput = viewChild.required<ElementRef<HTMLInputElement>>('searchInput');

  readonly teamName = input.required<string>();
  readonly memberIds = input.required<ReadonlySet<string>>();
  readonly busy = input(false);
  /** People the last attempt could not add, by email; they stay selected for another try. */
  readonly failures = input<readonly AddFailure[]>([]);

  readonly submitted = output<AddMembersRequest>();
  readonly dismissed = output<void>();

  protected readonly query = signal('');
  protected readonly selected = signal<readonly DirectoryUser[]>([]);
  protected readonly role = signal<TeamRole>('TeamMember');

  protected readonly results = toSignal(
    toObservable(this.query).pipe(
      map((query) => query.trim()),
      debounceTime(SEARCH_DEBOUNCE_MS),
      distinctUntilChanged(),
      switchMap((query) => this.searchFor(query)),
    ),
    { initialValue: { status: 'idle' } satisfies SearchState as SearchState },
  );

  /** The selection minus anyone who joined meanwhile, such as those a partly failed attempt did add. */
  protected readonly remaining = computed(() =>
    this.selected().filter((person) => !this.memberIds().has(person.id)),
  );

  protected readonly selectedIds = computed(
    () => new Set(this.remaining().map((person) => person.id)),
  );

  protected readonly failureFor = computed(
    () => new Map(this.failures().map((failure) => [failure.email, failure.message])),
  );

  protected readonly submitLabel = computed(() => {
    const count = this.remaining().length;

    return count === 0 ? 'Add members' : `Add ${count} member${count === 1 ? '' : 's'}`;
  });

  constructor() {
    afterNextRender(() => this._searchInput().nativeElement.focus());
  }

  protected initials(name: string): string {
    return initials(name);
  }

  protected onQuery(event: Event): void {
    this.query.set((event.target as HTMLInputElement).value);
  }

  protected isMember(person: DirectoryUser): boolean {
    return this.memberIds().has(person.id);
  }

  protected toggle(person: DirectoryUser): void {
    if (this.isMember(person) || this.busy()) {
      return;
    }

    this.selected.update((people) =>
      people.some((candidate) => candidate.id === person.id)
        ? people.filter((candidate) => candidate.id !== person.id)
        : [...people, person],
    );
  }

  protected unselect(person: DirectoryUser): void {
    if (!this.busy()) {
      this.selected.update((people) => people.filter((candidate) => candidate.id !== person.id));
    }
  }

  protected setRole(role: TeamRole): void {
    this.role.set(role);
  }

  protected submit(): void {
    const people = this.remaining();

    if (people.length > 0 && !this.busy()) {
      this.submitted.emit({ people, role: this.role() });
    }
  }

  private searchFor(query: string): Observable<SearchState> {
    if (query.length < DIRECTORY_QUERY_MIN_LENGTH) {
      return of({ status: 'idle' });
    }

    return this._directory.search(query).pipe(
      map((results): SearchState => ({ status: 'done', results })),
      catchError((error: unknown) =>
        of<SearchState>({
          status: 'error',
          message:
            problemStatus(error) === 429
              ? 'Too many searches at once. Wait a few seconds and try again.'
              : problemMessage(error, "We couldn't search the directory."),
        }),
      ),
      startWith<SearchState>({ status: 'loading' }),
    );
  }
}
