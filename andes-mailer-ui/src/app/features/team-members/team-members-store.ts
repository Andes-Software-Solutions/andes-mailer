import { computed, inject } from '@angular/core';
import { DirectoryUser } from '@core/api/directory-user.model';
import { IdempotentAction } from '@core/api/idempotency/idempotent-action';
import { problemMessage, problemStatus } from '@core/api/problem-message';
import { TeamMember, TeamRole } from '@core/api/team-member.model';
import { TeamMembersApi } from '@core/api/team-members-api';
import { SessionStore } from '@core/auth/session-store';
import { ToastStore } from '@core/feedback/toast-store';
import {
  patchState,
  signalStore,
  type,
  withComputed,
  withHooks,
  withMethods,
  withProps,
  withState,
} from '@ngrx/signals';
import {
  entityConfig,
  removeEntity,
  setAllEntities,
  setEntity,
  withEntities,
} from '@ngrx/signals/entities';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { EMPTY, catchError, firstValueFrom, pipe, switchMap, tap } from 'rxjs';

export type RoleFilter = 'all' | 'administrators' | 'members';

export type LoadStatus = 'idle' | 'loading' | 'loaded' | 'error';

/** What happened to a change the person asked for. */
export type ChangeOutcome = 'saved' | 'last-administrator' | 'failed';

export interface AddFailure {
  readonly email: string;
  readonly message: string;
}

interface TeamMembersState {
  teamId: string | null;
  status: LoadStatus;
  loadError: string | null;
  search: string;
  roleFilter: RoleFilter;
  /** Members added in this visit, highlighted until the page is left. */
  newIds: readonly string[];
  /** Members with a write in flight; their controls are disabled meanwhile. */
  pendingIds: readonly string[];
  adding: boolean;
}

const memberConfig = entityConfig({
  entity: type<TeamMember>(),
  selectId: (member) => member.userId,
});

/** At most this many adds run at once, so a large selection does not flood the directory lookups. */
const ADD_CONCURRENCY = 3;

/**
 * The active team's members and every change to them. Scoped to the Team members page, and loads
 * again whenever the active team changes. Every write carries an `Idempotency-Key`, kept per member
 * while the same change is retried, so a retry after a lost response never applies it twice.
 */
export const TeamMembersStore = signalStore(
  withState<TeamMembersState>({
    teamId: null,
    status: 'idle',
    loadError: null,
    search: '',
    roleFilter: 'all',
    newIds: [],
    pendingIds: [],
    adding: false,
  }),
  withEntities(memberConfig),
  withProps(() => ({
    _api: inject(TeamMembersApi),
    _session: inject(SessionStore),
    _toasts: inject(ToastStore),
    _changes: new Map<string, IdempotentAction>(),
    _adds: new Map<string, IdempotentAction>(),
  })),
  withComputed(({ entities, search, roleFilter }) => {
    const administratorCount = computed(
      () => entities().filter((member) => member.role === 'TeamAdministrator').length,
    );

    return {
      administratorCount,
      memberIds: computed(() => new Set(entities().map((member) => member.userId))),
      summary: computed(() => {
        const members = entities().length;
        const admins = administratorCount();

        return `${plural(members, 'member')} · ${plural(admins, 'administrator')}`;
      }),
      visible: computed(() => {
        const query = search().trim().toLowerCase();
        const filter = roleFilter();

        return entities()
          .filter(
            (member) =>
              filter === 'all' ||
              (filter === 'administrators') === (member.role === 'TeamAdministrator'),
          )
          .filter(
            (member) =>
              query.length === 0 ||
              member.fullName.toLowerCase().includes(query) ||
              member.email.toLowerCase().includes(query),
          );
      }),
    };
  }),
  withMethods((store) => {
    const teamId = (): string | null => store.teamId();

    const setPending = (userId: string, pending: boolean): void =>
      patchState(store, (state) => ({
        pendingIds: pending
          ? [...state.pendingIds, userId]
          : state.pendingIds.filter((id) => id !== userId),
      }));

    const actionFor = (map: Map<string, IdempotentAction>, id: string): IdempotentAction => {
      let action = map.get(id);

      if (!action) {
        action = new IdempotentAction();
        map.set(id, action);
      }

      return action;
    };

    const isSelf = (userId: string): boolean => store._session.user()?.id === userId;

    async function reload(): Promise<void> {
      const id = teamId();

      if (!id) {
        return;
      }

      try {
        const page = await firstValueFrom(store._api.list(id));

        if (teamId() === id) {
          patchState(store, setAllEntities([...page.items], memberConfig));
        }
      } catch {
        // The change already failed visibly; a stale list is the lesser problem.
      }
    }

    /** Adds one person; their key survives a failure, so a retry replays a request that did land. */
    async function addOne(
      id: string,
      person: DirectoryUser,
      role: TeamRole,
    ): Promise<AddFailure | null> {
      const request = { email: person.email, role };
      const action = actionFor(store._adds, person.email);

      try {
        const member = await firstValueFrom(store._api.add(id, request, action.keyFor(request)));

        action.settle();
        store._adds.delete(person.email);
        // The person may have switched team while this ran; the list on screen is then another team's.
        if (teamId() === id) {
          patchState(store, setEntity(member, memberConfig), (state) => ({
            newIds: [...state.newIds, member.userId],
          }));
        }

        return null;
      } catch (error) {
        action.settle(error);

        return {
          email: person.email,
          message: problemMessage(error, "We couldn't add this person."),
        };
      }
    }

    /** Loads the team's members; a newer team id cancels the request for the older one. */
    const load = rxMethod<string | null>(
      pipe(
        tap((id) =>
          patchState(store, setAllEntities([] as TeamMember[], memberConfig), {
            teamId: id,
            status: id ? 'loading' : 'idle',
            loadError: null,
            newIds: [],
            pendingIds: [],
          }),
        ),
        switchMap((id) =>
          id
            ? store._api.list(id).pipe(
                tap((page) =>
                  patchState(store, setAllEntities([...page.items], memberConfig), {
                    status: 'loaded',
                  }),
                ),
                // Caught inside the switchMap, so a failed load leaves the method listening for the next team.
                catchError((error: unknown) => {
                  patchState(store, {
                    status: 'error',
                    loadError: problemMessage(error, "We couldn't load the team's members."),
                  });

                  return EMPTY;
                }),
              )
            : EMPTY,
        ),
      ),
    );

    /** Whether `member` is the only team administrator, so demoting or removing them would leave none. */
    const isLastAdministrator = (member: TeamMember): boolean =>
      member.role === 'TeamAdministrator' && store.administratorCount() === 1;

    /** After a 409, the reloaded list says whether the conflict was the last-administrator rule. */
    const reconcile = (userId: string): ChangeOutcome => {
      const current = store.entityMap()[userId];

      return current && isLastAdministrator(current) ? 'last-administrator' : 'failed';
    };

    /** Undo for a removal: adds the person back with the role they had. */
    async function restore(id: string, member: TeamMember): Promise<void> {
      const failure = await addOne(
        id,
        {
          id: member.userId,
          firstName: member.firstName,
          lastName: member.lastName,
          fullName: member.fullName,
          email: member.email,
        },
        member.role,
      );

      store._toasts.show(
        failure
          ? { tone: 'error', message: failure.message }
          : { message: `${member.fullName} is back on the team` },
      );
    }

    return {
      load,

      isLastAdministrator,

      retryLoad(): void {
        load(teamId());
      },

      setSearch(search: string): void {
        patchState(store, { search });
      },

      setRoleFilter(roleFilter: RoleFilter): void {
        patchState(store, { roleFilter });
      },

      clearSearch(): void {
        patchState(store, { search: '', roleFilter: 'all' });
      },

      async changeRole(member: TeamMember, role: TeamRole): Promise<ChangeOutcome> {
        const id = teamId();
        const request = { role, version: member.version };

        if (!id || store.pendingIds().includes(member.userId)) {
          return 'failed';
        }

        if (request.role !== 'TeamAdministrator' && isLastAdministrator(member)) {
          return 'last-administrator';
        }

        const action = actionFor(store._changes, member.userId);
        setPending(member.userId, true);

        try {
          const updated = await firstValueFrom(
            store._api.update(id, member.userId, request, action.keyFor(request)),
          );

          action.settle();
          if (teamId() === id) {
            patchState(store, setEntity(updated, memberConfig));
          }
          store._toasts.show({ message: 'Role updated' });

          if (isSelf(member.userId)) {
            await store._session.refresh();
          }

          return 'saved';
        } catch (error) {
          action.settle(error);

          if (problemStatus(error) === 409) {
            await reload();
          }

          store._toasts.show({ tone: 'error', message: problemMessage(error) });

          return problemStatus(error) === 409 && request.role !== 'TeamAdministrator'
            ? reconcile(member.userId)
            : 'failed';
        } finally {
          setPending(member.userId, false);
        }
      },

      async remove(member: TeamMember): Promise<ChangeOutcome> {
        const id = teamId();

        if (!id || store.pendingIds().includes(member.userId)) {
          return 'failed';
        }

        if (isLastAdministrator(member)) {
          return 'last-administrator';
        }

        const action = actionFor(store._changes, `remove:${member.userId}`);
        setPending(member.userId, true);

        try {
          await firstValueFrom(store._api.remove(id, member.userId, action.keyFor(member.userId)));

          action.settle();
          if (teamId() === id) {
            patchState(store, removeEntity(member.userId));
          }

          if (isSelf(member.userId)) {
            // Leaving their own team ends their access to this page; the shell moves them on.
            await store._session.refresh();

            return 'saved';
          }

          store._toasts.show({
            message: `${member.fullName} removed`,
            action: { label: 'Undo', run: () => void restore(id, member) },
          });

          return 'saved';
        } catch (error) {
          action.settle(error);
          store._toasts.show({ tone: 'error', message: problemMessage(error) });

          if (problemStatus(error) === 409) {
            await reload();

            return reconcile(member.userId);
          }

          return 'failed';
        } finally {
          setPending(member.userId, false);
        }
      },

      /**
       * Adds every person, a few at a time. Returns the ones that failed, which the dialog keeps
       * selected; adding them again reuses their keys.
       */
      async addMany(
        people: readonly DirectoryUser[],
        role: TeamRole,
      ): Promise<readonly AddFailure[]> {
        const id = teamId();

        if (!id || store.adding() || people.length === 0) {
          return [];
        }

        patchState(store, { adding: true });

        try {
          const failures: AddFailure[] = [];
          const queue = [...people];
          const worker = async (): Promise<void> => {
            for (let person = queue.shift(); person; person = queue.shift()) {
              const failure = await addOne(id, person, role);

              if (failure) {
                failures.push(failure);
              }
            }
          };

          await Promise.all(
            Array.from({ length: Math.min(ADD_CONCURRENCY, people.length) }, worker),
          );

          const added = people.length - failures.length;

          if (added > 0) {
            store._toasts.show({ message: `${plural(added, 'member')} added` });
          }

          return failures;
        } finally {
          patchState(store, { adding: false });
        }
      },
    };
  }),
  withHooks({
    onInit(store) {
      store.load(computed(() => store._session.activeTeam()?.id ?? null));
    },
  }),
);

function plural(count: number, noun: string): string {
  return `${count} ${noun}${count === 1 ? '' : 's'}`;
}
