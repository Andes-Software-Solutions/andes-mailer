import { inject } from '@angular/core';
import { IdempotentAction } from '@core/api/idempotency/idempotent-action';
import { problemMessage } from '@core/api/problem-message';
import { CreateTeamRequest, Team, TeamSummary } from '@core/api/team.model';
import { TeamsApi } from '@core/api/teams-api';
import { SessionStore } from '@core/auth/session-store';
import { ToastStore } from '@core/feedback/toast-store';
import {
  patchState,
  signalStore,
  type,
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
import { firstValueFrom } from 'rxjs';

export type TeamsStatus = 'idle' | 'loading' | 'loaded' | 'error';

interface TeamsState {
  status: TeamsStatus;
  loadError: string | null;
  creating: boolean;
  deactivatingId: string | null;
}

const teamConfig = entityConfig({ entity: type<TeamSummary>(), selectId: (team) => team.id });

/**
 * Every team, for the global administrator who creates and deactivates them. Creating and
 * deactivating each carry the `Idempotency-Key` of their intent.
 */
export const TeamsStore = signalStore(
  withState<TeamsState>({ status: 'idle', loadError: null, creating: false, deactivatingId: null }),
  withEntities(teamConfig),
  withProps(() => ({
    _api: inject(TeamsApi),
    _session: inject(SessionStore),
    _toasts: inject(ToastStore),
    _create: new IdempotentAction(),
    _deactivations: new Map<string, IdempotentAction>(),
  })),
  withMethods((store) => ({
    async load(): Promise<void> {
      patchState(store, { status: 'loading', loadError: null });

      try {
        const page = await firstValueFrom(store._api.list());

        patchState(store, setAllEntities([...page.items], teamConfig), { status: 'loaded' });
      } catch (error) {
        patchState(store, {
          status: 'error',
          loadError: problemMessage(error, "We couldn't load the teams."),
        });
      }
    },

    /** Creates the team and returns it; on failure throws the response, for the dialog to show. */
    async create(request: CreateTeamRequest): Promise<Team> {
      patchState(store, { creating: true });

      try {
        const team = await firstValueFrom(
          store._api.create(request, store._create.keyFor(request)),
        );

        store._create.settle();
        const summary: TeamSummary = {
          id: team.id,
          name: team.name,
          description: team.description,
          serviceAccountEmail: team.serviceAccountEmail,
          avatarColor: team.avatarColor,
          memberCount: team.memberCount,
          role: null,
        };

        patchState(store, setEntity(summary, teamConfig));
        await store._session.refresh();
        store._toasts.show({ message: `${team.name} created` });

        return team;
      } catch (error) {
        store._create.settle(error);

        throw error;
      } finally {
        patchState(store, { creating: false });
      }
    },

    async deactivate(team: TeamSummary): Promise<boolean> {
      if (store.deactivatingId()) {
        return false;
      }

      let action = store._deactivations.get(team.id);

      if (!action) {
        action = new IdempotentAction();
        store._deactivations.set(team.id, action);
      }

      patchState(store, { deactivatingId: team.id });

      try {
        await firstValueFrom(store._api.deactivate(team.id, action.keyFor(team.id)));

        action.settle();
        store._deactivations.delete(team.id);
        patchState(store, removeEntity(team.id));
        await store._session.refresh();
        store._toasts.show({ message: `${team.name} deactivated` });

        return true;
      } catch (error) {
        action.settle(error);
        store._toasts.show({
          tone: 'error',
          message: problemMessage(error, "We couldn't deactivate the team."),
        });

        return false;
      } finally {
        patchState(store, { deactivatingId: null });
      }
    },
  })),
  withHooks({
    onInit(store) {
      void store.load();
    },
  }),
);
