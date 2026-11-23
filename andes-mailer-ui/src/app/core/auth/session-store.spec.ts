import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { EventType, IPublicClientApplication, InteractionType } from '@azure/msal-browser';
import { provideTestAppConfig, testApiScope } from '@testing/app-config';
import { eventCallbacks, provideFakeMsal, signedInMsal } from '@testing/msal';
import {
  adminUser,
  communicationsTeam,
  hrTeam,
  meUrl,
  memberUser,
  noAccessUser,
  seedSession,
} from '@testing/session';
import { vi } from 'vitest';
import { SessionStore } from './session-store';

describe('SessionStore', () => {
  let backend: HttpTestingController;

  function setup(
    instance: IPublicClientApplication = signedInMsal(),
  ): InstanceType<typeof SessionStore> {
    TestBed.configureTestingModule({
      providers: [
        provideTestAppConfig(),
        provideFakeMsal(instance),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    backend = TestBed.inject(HttpTestingController);

    return TestBed.inject(SessionStore);
  }

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('loading', () => {
    it('asks the API once for concurrent callers', async () => {
      const store = setup();

      const first = store.ensureLoaded();
      const second = store.ensureLoaded();
      backend.expectOne(meUrl).flush(memberUser);
      await Promise.all([first, second]);

      expect(store.status()).toBe('loaded');
      expect(store.user()).toEqual(memberUser);
      backend.verify();
    });

    it('does not ask again once loaded', async () => {
      const store = setup();

      const load = store.ensureLoaded();
      backend.expectOne(meUrl).flush(memberUser);
      await load;
      await store.ensureLoaded();

      backend.verify();
    });

    it('records a failure and loads again on retry', async () => {
      vi.spyOn(console, 'error').mockImplementation(() => undefined);
      const store = setup();

      const failed = store.ensureLoaded();
      backend.expectOne(meUrl).flush(null, { status: 500, statusText: 'Server Error' });
      await failed;

      expect(store.status()).toBe('error');
      expect(store.signInPhase()).toBe('error');

      const retried = store.ensureLoaded();
      backend.expectOne(meUrl).flush(memberUser);
      await retried;

      expect(store.status()).toBe('loaded');
    });

    it('treats a response of the wrong shape as a failure', async () => {
      vi.spyOn(console, 'error').mockImplementation(() => undefined);
      const store = setup();

      const load = store.ensureLoaded();
      backend.expectOne(meUrl).flush({ id: '1', name: 'Ada Lovelace' });
      await load;

      expect(store.status()).toBe('error');
      expect(store.user()).toBeNull();
    });
  });

  describe('access', () => {
    it('reports an inactive account as having no access', () => {
      const store = setup();

      seedSession(noAccessUser);

      expect(store.hasAccess()).toBe(false);
      expect(store.permissions().size).toBe(0);
    });

    it('works in the first team until another is picked', () => {
      const store = setup();

      seedSession(memberUser);

      expect(store.activeTeam()).toEqual(hrTeam);
      expect(store.can('ComposeTeamEmails')).toBe(true);
      expect(store.can('ModifyTeamUser')).toBe(false);
    });

    it('follows the picked team and remembers it per user', () => {
      const store = setup();
      seedSession(memberUser);

      store.selectTeam(communicationsTeam.id);

      expect(store.activeTeam()).toEqual(communicationsTeam);
      expect(store.can('ModifyTeamUser')).toBe(true);
      expect(localStorage.getItem(`andes-mailer.team.${memberUser.id}`)).toBe(
        communicationsTeam.id,
      );
    });

    it('restores the remembered team on load', async () => {
      localStorage.setItem(`andes-mailer.team.${memberUser.id}`, communicationsTeam.id);
      const store = setup();

      const load = store.ensureLoaded();
      backend.expectOne(meUrl).flush(memberUser);
      await load;

      expect(store.activeTeam()?.id).toBe(communicationsTeam.id);
    });

    it('ignores a team the user cannot see', () => {
      const store = setup();
      seedSession(memberUser);

      store.selectTeam('not-a-team');

      expect(store.activeTeam()).toEqual(hrTeam);
    });

    it('adds global permissions to the team ones', () => {
      const store = setup();

      seedSession({ ...memberUser, permissions: ['CreateTeam'] });

      expect(store.can(['CreateTeam', 'ComposeTeamEmails'], 'all')).toBe(true);
    });

    it('needs one permission in any mode and every one in all mode', () => {
      const store = setup();
      seedSession(memberUser);

      expect(store.can(['ModifyTeamUser', 'ComposeTeamEmails'], 'any')).toBe(true);
      expect(store.can(['ModifyTeamUser', 'ComposeTeamEmails'], 'all')).toBe(false);
      expect(store.can([])).toBe(true);
    });

    it('marks a global administrator', () => {
      const store = setup();

      seedSession(adminUser);

      expect(store.isAdministrator()).toBe(true);
    });
  });

  describe('sign-in phase', () => {
    it('shows the redirect while MSAL sends the person to Microsoft', () => {
      const events = eventCallbacks();
      const store = setup(signedInMsal({ addEventCallback: events.addEventCallback }));

      for (const callback of events.callbacks) {
        callback({
          eventType: EventType.ACQUIRE_TOKEN_START,
          interactionType: InteractionType.Redirect,
          payload: null,
          error: null,
          timestamp: Date.now(),
          correlationId: '00000000-0000-0000-0000-000000000000',
        });
      }

      expect(store.signInPhase()).toBe('redirecting');
    });

    it('shows signing in while the account loads', () => {
      const store = setup();

      void store.ensureLoaded();

      expect(store.signInPhase()).toBe('signing-in');
      backend.expectOne(meUrl).flush(memberUser);
    });
  });

  describe('redirects', () => {
    it('signs in towards home, offering the account picker when asked', async () => {
      const loginRedirect = vi.fn(() => Promise.resolve());
      const store = setup(signedInMsal({ loginRedirect }));

      await store.signIn({ selectAccount: true });

      expect(loginRedirect).toHaveBeenCalledWith(
        expect.objectContaining({
          scopes: [testApiScope],
          prompt: 'select_account',
          redirectStartPage: new URL('/', document.baseURI).href,
        }),
      );
    });
  });
});
