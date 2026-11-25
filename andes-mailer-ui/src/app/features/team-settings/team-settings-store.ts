import { computed, inject } from '@angular/core';
import { IdempotentAction } from '@core/api/idempotency/idempotent-action';
import { problemMessage, problemStatus } from '@core/api/problem-message';
import { Team, UpdateTeamRequest } from '@core/api/team.model';
import { TeamMembersApi } from '@core/api/team-members-api';
import { TeamsApi } from '@core/api/teams-api';
import { SessionStore } from '@core/auth/session-store';
import { ToastStore } from '@core/feedback/toast-store';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withProps,
  withState,
} from '@ngrx/signals';
import { rxMethod } from '@ngrx/signals/rxjs-interop';
import { EMPTY, catchError, firstValueFrom, pipe, switchMap, tap } from 'rxjs';

export type SettingsStatus = 'idle' | 'loading' | 'loaded' | 'error';

export type SaveOutcome = 'saved' | 'conflict' | 'failed';

interface TeamSettingsState {
  team: Team | null;
  status: SettingsStatus;
  loadError: string | null;
  saving: boolean;
  leaving: boolean;
}

/**
 * The active team's settings. Scoped to the Team settings page and loaded again when the active
 * team changes. Saving and leaving each send the `Idempotency-Key` of their intent, so pressing the
 * button again after a lost response replays the first result.
 */
export const TeamSettingsStore = signalStore(
  withState<TeamSettingsState>({
    team: null,
    status: 'idle',
    loadError: null,
    saving: false,
    leaving: false,
  }),
  withProps(() => ({
    _teams: inject(TeamsApi),
    _members: inject(TeamMembersApi),
    _session: inject(SessionStore),
    _toasts: inject(ToastStore),
    _save: new IdempotentAction(),
    _leave: new IdempotentAction(),
  })),
  withComputed(({ team, _session }) => ({
    /** The mailbox decides who mail comes from, so only a global administrator changes it. */
    canEditMailbox: computed(() => _session.isAdministrator()),
    canEdit: computed(() => _session.can('ModifyTeam')),
    /** Leaving needs a membership; a global administrator may see the team without one. */
    isMember: computed(() => _session.activeTeam()?.role != null),
    /** The only team administrator cannot leave, or the team would have none. */
    isOnlyAdministrator: computed(
      () =>
        _session.activeTeam()?.role === 'TeamAdministrator' &&
        (team()?.administratorCount ?? 0) <= 1,
    ),
  })),
  withMethods((store) => {
    const load = rxMethod<string | null>(
      pipe(
        tap((id) =>
          patchState(store, { team: null, status: id ? 'loading' : 'idle', loadError: null }),
        ),
        switchMap((id) =>
          id
            ? store._teams.get(id).pipe(
                tap((team) => patchState(store, { team, status: 'loaded' })),
                catchError((error: unknown) => {
                  patchState(store, {
                    status: 'error',
                    loadError: problemMessage(error, "We couldn't load the team's settings."),
                  });

                  return EMPTY;
                }),
              )
            : EMPTY,
        ),
      ),
    );

    return {
      load,

      reload(): void {
        load(store.team()?.id ?? store._session.activeTeam()?.id ?? null);
      },

      async save(request: Omit<UpdateTeamRequest, 'version'>): Promise<SaveOutcome> {
        const team = store.team();

        if (!team || store.saving()) {
          return 'failed';
        }

        const body: UpdateTeamRequest = { ...request, version: team.version };
        patchState(store, { saving: true });

        try {
          const saved = await firstValueFrom(
            store._teams.update(team.id, body, store._save.keyFor(body)),
          );

          store._save.settle();
          patchState(store, { team: saved });
          store._toasts.show({ message: 'Settings saved' });

          // The switcher shows the name and colour.
          if (saved.name !== team.name || saved.avatarColor !== team.avatarColor) {
            await store._session.refresh();
          }

          return 'saved';
        } catch (error) {
          store._save.settle(error);
          store._toasts.show({
            tone: 'error',
            message: problemMessage(error, "We couldn't save the settings."),
          });

          if (problemStatus(error) !== 409) {
            return 'failed';
          }

          // A 409 is also a taken name or mailbox, which the person fixes in the form. Only a new row
          // version means someone else saved the team.
          const fresh = await firstValueFrom(store._teams.get(team.id)).catch(() => null);

          return fresh !== null && fresh.version !== team.version ? 'conflict' : 'failed';
        } finally {
          patchState(store, { saving: false });
        }
      },

      async leave(): Promise<boolean> {
        const team = store.team();

        if (!team || store.leaving()) {
          return false;
        }

        patchState(store, { leaving: true });

        try {
          await firstValueFrom(store._members.leave(team.id, store._leave.keyFor(team.id)));

          store._leave.settle();
          store._toasts.show({ message: `You left ${team.name}` });
          await store._session.refresh();

          return true;
        } catch (error) {
          store._leave.settle(error);
          store._toasts.show({
            tone: 'error',
            message: problemMessage(error, "We couldn't remove you from the team."),
          });

          return false;
        } finally {
          patchState(store, { leaving: false });
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
