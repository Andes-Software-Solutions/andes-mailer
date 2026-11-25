import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { DirectoryUser } from '@core/api/directory-user.model';
import { IDEMPOTENCY_KEY } from '@core/api/idempotency/idempotency-key';
import { TeamMember } from '@core/api/team-member.model';
import { ToastStore } from '@core/feedback/toast-store';
import {
  communicationsTeam,
  memberUser,
  provideSessionTesting,
  seedSession,
} from '@testing/session';
import { TeamMembersStore } from './team-members-store';

const membersUrl = `/api/teams/${communicationsTeam.id}/members`;

function member(overrides: Partial<TeamMember> = {}): TeamMember {
  return {
    userId: 'u-1',
    firstName: 'Priya',
    lastName: 'Shah',
    fullName: 'Priya Shah',
    email: 'priya@example.com',
    role: 'TeamMember',
    dateCreated: '2026-11-01T10:00:00Z',
    dateModified: null,
    version: 'AAAAAAAAB9E=',
    ...overrides,
  };
}

const self = member({
  userId: memberUser.id,
  fullName: 'Maria Lopez',
  email: 'maria.lopez@example.com',
  role: 'TeamAdministrator',
});

function person(id: string, email: string): DirectoryUser {
  return { id, firstName: 'New', lastName: id, fullName: `New ${id}`, email };
}

describe('TeamMembersStore', () => {
  let backend: HttpTestingController;

  function setup(
    members: readonly TeamMember[] = [self, member()],
  ): InstanceType<typeof TeamMembersStore> {
    TestBed.configureTestingModule({ providers: [...provideSessionTesting(), TeamMembersStore] });
    seedSession(memberUser, communicationsTeam.id);
    backend = TestBed.inject(HttpTestingController);

    const store = TestBed.inject(TeamMembersStore);
    TestBed.tick();
    backend
      .expectOne((request) => request.url === membersUrl && request.method === 'GET')
      .flush({ items: members, page: 1, pageSize: 100, totalCount: members.length });

    return store;
  }

  afterEach(() => backend.verify());

  it("loads the active team's members and summarises them", () => {
    const store = setup();

    expect(store.status()).toBe('loaded');
    expect(store.summary()).toBe('2 members · 1 administrator');
  });

  it('filters by role and search together', () => {
    const store = setup();

    store.setRoleFilter('members');
    store.setSearch('PRIYA');

    expect(store.visible().map((m) => m.userId)).toEqual(['u-1']);
  });

  it('refuses to demote the only administrator without asking the API', async () => {
    const store = setup();

    await expect(store.changeRole(self, 'TeamMember')).resolves.toBe('last-administrator');
    backend.expectNone(`${membersUrl}/${self.userId}`);
  });

  it('changes a role with an idempotency key', async () => {
    const store = setup();
    const target = member();

    const outcome = store.changeRole(target, 'TeamAdministrator');
    const request = backend.expectOne(`${membersUrl}/u-1`);
    request.flush({ ...target, role: 'TeamAdministrator', version: 'AAAAAAAAB9F=' });

    await expect(outcome).resolves.toBe('saved');
    expect(request.request.body).toEqual({ role: 'TeamAdministrator', version: target.version });
    expect(request.request.context.get(IDEMPOTENCY_KEY)).toEqual(expect.any(String));
    expect(store.administratorCount()).toBe(2);
  });

  it('removes a member and puts them back with Undo', async () => {
    const store = setup();
    const toasts = TestBed.inject(ToastStore);
    const target = member();

    const removed = store.remove(target);
    backend.expectOne(`${membersUrl}/u-1`).flush(null, { status: 204, statusText: 'No Content' });
    await removed;

    expect(store.entityMap()['u-1']).toBeUndefined();
    const toast = toasts.toasts().at(-1);
    expect(toast?.message).toBe('Priya Shah removed');

    toasts.act(toast!.id);
    const undo = backend.expectOne(
      (request) => request.url === membersUrl && request.method === 'POST',
    );
    expect(undo.request.body).toEqual({
      email: 'priya@example.com',
      role: 'TeamMember',
    });
    undo.flush(target);
    await Promise.resolve();
    await Promise.resolve();

    expect(store.entityMap()['u-1']).toBeDefined();
  });

  it('keeps the people it could not add, and retries them with the same key', async () => {
    const store = setup();
    const ada = person('ada', 'ada@example.com');
    const bo = person('bo', 'bo@example.com');

    const first = store.addMany([ada, bo], 'TeamMember');
    const requests = backend.match(
      (request) => request.url === membersUrl && request.method === 'POST',
    );
    const adaRequest = requests.find((r) => r.request.body.email === ada.email)!;
    const boRequest = requests.find((r) => r.request.body.email === bo.email)!;
    adaRequest.flush(member({ userId: 'ada', fullName: 'New ada', email: ada.email }));
    boRequest.error(new ProgressEvent('error'), { status: 0 });

    const failures = await first;

    expect(failures.map((failure) => failure.email)).toEqual([bo.email]);
    expect(store.newIds()).toEqual(['ada']);

    const retry = store.addMany([bo], 'TeamMember');
    const again = backend.expectOne(
      (request) => request.url === membersUrl && request.method === 'POST',
    );
    again.flush(member({ userId: 'bo', fullName: 'New bo', email: bo.email }));
    await retry;

    expect(again.request.context.get(IDEMPOTENCY_KEY)).toBe(
      boRequest.request.context.get(IDEMPOTENCY_KEY),
    );
    expect(again.request.context.get(IDEMPOTENCY_KEY)).not.toBe(
      adaRequest.request.context.get(IDEMPOTENCY_KEY),
    );
  });
});
