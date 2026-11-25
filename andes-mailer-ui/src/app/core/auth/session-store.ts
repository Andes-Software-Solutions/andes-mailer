import { DOCUMENT, Location } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MsalBroadcastService, MsalService } from '@azure/msal-angular';
import { InteractionStatus } from '@azure/msal-browser';
import { CurrentUser, CurrentUserTeam } from '@core/api/current-user.model';
import { Permission } from '@core/api/permission.model';
import { APP_CONFIG } from '@core/config/app-config-token';
import { routePaths } from '@core/navigation/route-paths';
import { safeLocalStorage } from '@core/theme/theme-preference';
import {
  PartialStateUpdater,
  patchState,
  signalStore,
  withComputed,
  withMethods,
  withProps,
  withState,
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';

export type SessionStatus = 'idle' | 'loading' | 'loaded' | 'error';

/** What the sign-in splash shows; `none` hides it. */
export type SignInPhase = 'redirecting' | 'signing-in' | 'error' | 'none';

/** `any`: one of the permissions is enough. `all`: every one is needed. */
export type PermissionMode = 'any' | 'all';

export interface SignInOptions {
  /** Lets the person pick another account instead of signing in again silently. */
  readonly selectAccount?: boolean;
}

interface SessionState {
  user: CurrentUser | null;
  status: SessionStatus;
  activeTeamId: string | null;
  /** Where a navigation was going when the account failed to load, so a retry can resume it. */
  retryUrl: string | null;
}

const ME_URL = '/api/users/me';

const TEAM_STORAGE_PREFIX = 'andes-mailer.team.';

/**
 * The signed-in user and what they may do: their global permissions together with those of the
 * team they are working in.
 */
export const SessionStore = signalStore(
  { providedIn: 'root' },
  withState<SessionState>({ user: null, status: 'idle', activeTeamId: null, retryUrl: null }),
  withProps(() => ({
    _http: inject(HttpClient),
    _msal: inject(MsalService),
    _scopes: [...inject(APP_CONFIG).auth.apiScopes],
    _document: inject(DOCUMENT),
    _location: inject(Location),
    _interaction: toSignal(inject(MsalBroadcastService).inProgress$, {
      initialValue: InteractionStatus.Startup,
    }),
  })),
  withComputed(({ user, status, activeTeamId, _interaction }) => {
    const teams = computed<readonly CurrentUserTeam[]>(() => user()?.teams ?? []);
    const activeTeam = computed<CurrentUserTeam | null>(() => {
      const all = teams();

      return all.find((team) => team.id === activeTeamId()) ?? all[0] ?? null;
    });

    return {
      teams,
      activeTeam,
      hasAccess: computed(() => user()?.isActive === true),
      isAdministrator: computed(() => user()?.roles.includes('Administrator') ?? false),
      permissions: computed<ReadonlySet<Permission>>(
        () => new Set([...(user()?.permissions ?? []), ...(activeTeam()?.permissions ?? [])]),
      ),
      signInPhase: computed<SignInPhase>(() => {
        if (status() === 'error') {
          return 'error';
        }

        switch (_interaction()) {
          // MSAL v5 reports a redirect or popup sign-in as AcquireToken; silent token work raises no status.
          case InteractionStatus.AcquireToken:
            return 'redirecting';
          case InteractionStatus.Startup:
          case InteractionStatus.HandleRedirect:
            return 'signing-in';
          default:
            return status() === 'loading' ? 'signing-in' : 'none';
        }
      }),
    };
  }),
  withMethods((store) => {
    let inFlight: Promise<void> | null = null;

    const storage = (): Storage | undefined => safeLocalStorage(store._document.defaultView);

    const appUrl = (path: string): string =>
      new URL(store._location.prepareExternalUrl(path), store._document.baseURI).href;

    async function fetchUser(): Promise<CurrentUser> {
      const user = await firstValueFrom(store._http.get<unknown>(ME_URL));

      if (!isCurrentUser(user)) {
        throw new TypeError(`${ME_URL} returned an unexpected shape.`);
      }

      return user;
    }

    async function load(): Promise<void> {
      patchState(store, { status: 'loading' });

      try {
        const user = await fetchUser();

        patchState(store, loaded(user, readStoredTeam(storage(), user.id)));
      } catch (cause) {
        console.error(cause);
        patchState(store, { status: 'error' });
      }
    }

    return {
      /** Loads the user once; concurrent callers share the request, and a failure can be retried. */
      ensureLoaded(): Promise<void> {
        if (store.status() === 'loaded') {
          return Promise.resolve();
        }

        inFlight ??= load().finally(() => {
          inFlight = null;
        });

        return inFlight;
      },

      /**
       * Reads the account again after a change to the user's teams, such as creating, renaming, or
       * leaving one, without the sign-in splash. Keeps the active team while it still exists;
       * otherwise the first team becomes active. A failure keeps what the store holds.
       */
      async refresh(): Promise<void> {
        try {
          const user = await fetchUser();

          patchState(store, { user, status: 'loaded' });
        } catch (cause) {
          console.error(cause);
        }
      },

      /** Reactive: reads the permission signal, so templates and computeds follow team switches. */
      can(required: Permission | readonly Permission[], mode: PermissionMode = 'any'): boolean {
        const held = store.permissions();
        const list: readonly Permission[] = typeof required === 'string' ? [required] : required;

        if (list.length === 0) {
          return true;
        }

        return mode === 'all'
          ? list.every((permission) => held.has(permission))
          : list.some((permission) => held.has(permission));
      },

      /** Called by a guard that cancelled a navigation because the account failed to load. */
      holdForRetry(url: string): void {
        patchState(store, { retryUrl: url });
      },

      /** Returns `false`, changing nothing, for a team the account does not list. */
      selectTeam(teamId: string): boolean {
        const user = store.user();

        if (!user || !store.teams().some((team) => team.id === teamId)) {
          return false;
        }

        patchState(store, { activeTeamId: teamId });
        writeStoredTeam(storage(), user.id, teamId);

        return true;
      },

      /** Rejects when MSAL cannot start the redirect, for example while another one is pending. */
      async signIn(options: SignInOptions = {}): Promise<void> {
        await firstValueFrom(
          store._msal.loginRedirect({
            scopes: store._scopes,
            // MSAL returns to the page a sign-in names as its start; the home page, never a failure page.
            redirectStartPage: appUrl(routePaths.home),
            prompt: options.selectAccount ? 'select_account' : undefined,
          }),
        );
      },
    };
  }),
);

function loaded(user: CurrentUser, storedTeamId: string | null): PartialStateUpdater<SessionState> {
  return () => ({ user, status: 'loaded', activeTeamId: storedTeamId, retryUrl: null });
}

function readStoredTeam(storage: Storage | undefined, userId: string): string | null {
  try {
    return storage?.getItem(TEAM_STORAGE_PREFIX + userId) ?? null;
  } catch {
    return null;
  }
}

function writeStoredTeam(storage: Storage | undefined, userId: string, teamId: string): void {
  try {
    storage?.setItem(TEAM_STORAGE_PREFIX + userId, teamId);
  } catch {
    // Storage blocked: the choice lasts for this page only.
  }
}

/** The response type is only an assertion; check what the rest of the app relies on. */
function isCurrentUser(value: unknown): value is CurrentUser {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const user = value as Partial<Record<keyof CurrentUser, unknown>>;

  return (
    typeof user.id === 'string' &&
    typeof user.name === 'string' &&
    typeof user.isActive === 'boolean' &&
    Array.isArray(user.roles) &&
    Array.isArray(user.permissions) &&
    Array.isArray(user.teams) &&
    user.teams.every(
      (team: unknown) =>
        typeof team === 'object' &&
        team !== null &&
        typeof (team as { id?: unknown }).id === 'string' &&
        Array.isArray((team as { permissions?: unknown }).permissions),
    )
  );
}
