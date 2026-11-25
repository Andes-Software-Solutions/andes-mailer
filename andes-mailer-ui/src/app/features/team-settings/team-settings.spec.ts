import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { CurrentUser } from '@core/api/current-user.model';
import { IDEMPOTENCY_KEY } from '@core/api/idempotency/idempotency-key';
import { Team } from '@core/api/team.model';
import {
  adminUser,
  communicationsTeam,
  meUrl,
  memberUser,
  provideSessionTesting,
  seedSession,
} from '@testing/session';
import { vi } from 'vitest';
import { TeamSettings } from './team-settings';

const teamUrl = `/api/teams/${communicationsTeam.id}`;

const team: Team = {
  id: communicationsTeam.id,
  name: 'Corporate Communications',
  description: null,
  serviceAccountEmail: 'comms@example.com',
  avatarColor: 'Mint',
  defaultSignature: null,
  confirmRecipientsAbove: 50,
  memberCount: 3,
  administratorCount: 1,
  dateCreated: '2026-11-01T10:00:00Z',
  dateModified: null,
  version: 'AAAAAAAAB9E=',
};

describe('TeamSettings', () => {
  let backend: HttpTestingController;

  async function render(
    user: CurrentUser,
    loaded: Team = team,
  ): Promise<ComponentFixture<TeamSettings>> {
    TestBed.configureTestingModule({ providers: [...provideSessionTesting(), provideRouter([])] });
    seedSession(user, communicationsTeam.id);
    backend = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(TeamSettings);
    TestBed.tick();
    backend.expectOne(teamUrl).flush(loaded);
    await fixture.whenStable();

    return fixture;
  }

  function element(fixture: ComponentFixture<TeamSettings>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function input(fixture: ComponentFixture<TeamSettings>, id: string): HTMLInputElement {
    return element(fixture).querySelector<HTMLInputElement>(`#${id}`)!;
  }

  afterEach(() => {
    backend
      .match((r) => r.url === '/api/users/directory')
      .filter((r) => !r.cancelled)
      .forEach((r) => r.flush([]));
    backend.verify();
  });

  it('shows the saved settings', async () => {
    const fixture = await render(memberUser);

    expect(input(fixture, 'team-name').value).toBe('Corporate Communications');
    expect(input(fixture, 'confirm-above').value).toBe('50');
  });

  it('lets a team administrator edit everything but the mailbox', async () => {
    const fixture = await render(memberUser);

    expect(input(fixture, 'team-name').disabled).toBe(false);
    expect(input(fixture, 'team-mailbox').disabled).toBe(true);
  });

  it('lets a global administrator change the mailbox', async () => {
    const fixture = await render(adminUser);

    expect(input(fixture, 'team-mailbox').disabled).toBe(false);
  });

  it('offers to save only once something changed, and saves with an idempotency key', async () => {
    const fixture = await render(memberUser);
    expect(element(fixture).querySelector('.unsaved-bar')).toBeNull();

    const name = input(fixture, 'team-name');
    name.value = 'Comms';
    name.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    const bar = element(fixture).querySelector('.unsaved-bar');
    expect(bar?.textContent).toContain('You have unsaved changes');

    bar!.querySelector<HTMLButtonElement>('button[type="submit"]')!.click();
    await fixture.whenStable();
    const request = backend.expectOne((r) => r.url === teamUrl && r.method === 'PUT');

    expect(request.request.body).toMatchObject({
      name: 'Comms',
      version: team.version,
    });
    expect(request.request.context.get(IDEMPOTENCY_KEY)).toEqual(expect.any(String));

    request.flush({ ...team, name: 'Comms', version: 'AAAAAAAAB9F=' });
    await fixture.whenStable();
    // A rename refreshes the account, so the team switcher shows the new name.
    backend.expectOne(meUrl).flush(memberUser);
    await fixture.whenStable();

    expect(element(fixture).querySelector('.unsaved-bar')).toBeNull();
  });

  it('puts the saved values back on Discard', async () => {
    const fixture = await render(memberUser);
    const name = input(fixture, 'team-name');
    name.value = 'Something else';
    name.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    element(fixture).querySelector<HTMLButtonElement>('.unsaved-bar .btn-outline')!.click();
    await fixture.whenStable();

    expect(input(fixture, 'team-name').value).toBe('Corporate Communications');
    expect(fixture.componentInstance.hasUnsavedChanges()).toBe(false);
  });

  it('blocks leaving for the only administrator', async () => {
    const fixture = await render(memberUser);
    const leave = [...element(fixture).querySelectorAll<HTMLButtonElement>('button')].find(
      (button) => button.textContent?.trim() === 'Leave team',
    );

    expect(leave?.disabled).toBe(true);
    expect(element(fixture).textContent).toContain("You're the only administrator.");
  });

  it('hides leaving from a global administrator who is not a member', async () => {
    const fixture = await render(adminUser);

    expect(element(fixture).textContent).not.toContain('Danger zone');
  });

  async function edit(fixture: ComponentFixture<TeamSettings>, name: string): Promise<void> {
    const field = input(fixture, 'team-name');
    field.value = name;
    field.dispatchEvent(new Event('input'));
    await fixture.whenStable();
  }

  async function clickSave(fixture: ComponentFixture<TeamSettings>): Promise<void> {
    element(fixture)
      .querySelector<HTMLButtonElement>('.unsaved-bar button[type="submit"]')!
      .click();
    await fixture.whenStable();
  }

  it('says replies go to the mailbox the team sends from', async () => {
    const fixture = await render(memberUser);

    expect(element(fixture).querySelector('.replies-note')?.textContent).toContain(
      'Replies go to comms@example.com',
    );
  });

  it('offers to reload after someone else saved the team', async () => {
    vi.spyOn(console, 'error').mockImplementation(() => undefined);
    const fixture = await render(memberUser);
    await edit(fixture, 'Comms');

    await clickSave(fixture);
    backend
      .expectOne((r) => r.url === teamUrl && r.method === 'PUT')
      .flush(
        { detail: 'The team was changed by another request.' },
        { status: 409, statusText: 'Conflict' },
      );
    await vi.waitFor(() =>
      backend
        .expectOne((r) => r.url === teamUrl && r.method === 'GET')
        .flush({ ...team, version: 'AAAAAAAAB9H=' }),
    );
    await vi.waitFor(async () => {
      await fixture.whenStable();
      expect(element(fixture).textContent).toContain('Someone else changed these settings');
    });

    [...element(fixture).querySelectorAll<HTMLButtonElement>('button')]
      .find((button) => button.textContent?.includes('Reload settings'))!
      .click();
    await fixture.whenStable();
    backend.expectOne(teamUrl).flush({ ...team, name: 'Their name', version: 'AAAAAAAAB9G=' });
    await fixture.whenStable();

    expect(input(fixture, 'team-name').value).toBe('Their name');
    expect(element(fixture).textContent).not.toContain('Someone else changed these settings');
  });

  it('keeps the edits and Save for a name another team already has', async () => {
    const fixture = await render(memberUser);
    await edit(fixture, 'Press Office');

    await clickSave(fixture);
    backend
      .expectOne((r) => r.url === teamUrl && r.method === 'PUT')
      .flush(
        { detail: 'A team with this name already exists.' },
        { status: 409, statusText: 'Conflict' },
      );
    await vi.waitFor(() =>
      backend.expectOne((r) => r.url === teamUrl && r.method === 'GET').flush(team),
    );
    await vi.waitFor(async () => {
      await fixture.whenStable();
      expect(
        element(fixture).querySelector<HTMLButtonElement>('.unsaved-bar button[type="submit"]')
          ?.disabled,
      ).toBe(false);
    });

    expect(element(fixture).textContent).not.toContain('Someone else changed these settings');
    expect(input(fixture, 'team-name').value).toBe('Press Office');
  });

  it('leaves the team and goes home', async () => {
    const fixture = await render(memberUser, { ...team, administratorCount: 2 });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    [...element(fixture).querySelectorAll<HTMLButtonElement>('button')]
      .find((button) => button.textContent?.trim() === 'Leave team')!
      .click();
    await fixture.whenStable();
    [...element(fixture).querySelectorAll<HTMLButtonElement>('dialog button')]
      .find((button) => button.textContent?.trim() === 'Leave team')!
      .click();
    await fixture.whenStable();

    const request = backend.expectOne(`${teamUrl}/members/me`);
    expect(request.request.method).toBe('DELETE');
    expect(request.request.context.get(IDEMPOTENCY_KEY)).toEqual(expect.any(String));
    request.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();
    backend.expectOne(meUrl).flush({ ...memberUser, teams: memberUser.teams.slice(0, 1) });
    await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/'));
    TestBed.tick();
    backend.match(() => true).forEach((pending) => pending.flush(team));
  });
});
