import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { IDEMPOTENCY_KEY } from '@core/api/idempotency/idempotency-key';
import { Team, TeamSummary } from '@core/api/team.model';
import { adminUser, meUrl, provideSessionTesting, seedSession } from '@testing/session';
import { vi } from 'vitest';
import { Teams } from './teams';

const summary: TeamSummary = {
  id: 'team-1',
  name: 'Press Office',
  description: 'Media enquiries',
  serviceAccountEmail: 'press@example.com',
  avatarColor: 'Ocean',
  memberCount: 4,
  role: null,
};

describe('Teams', () => {
  let backend: HttpTestingController;

  async function render(
    items: readonly TeamSummary[] = [summary],
  ): Promise<ComponentFixture<Teams>> {
    TestBed.configureTestingModule({ providers: [...provideSessionTesting(), provideRouter([])] });
    seedSession(adminUser);
    backend = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(Teams);
    backend
      .expectOne((r) => r.url === '/api/teams' && r.method === 'GET')
      .flush({ items, page: 1, pageSize: 100, totalCount: items.length });
    await fixture.whenStable();

    return fixture;
  }

  function element(fixture: ComponentFixture<Teams>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function button(fixture: ComponentFixture<Teams>, text: string): HTMLButtonElement {
    return [...element(fixture).querySelectorAll<HTMLButtonElement>('button')].find((candidate) =>
      candidate.textContent?.includes(text),
    )!;
  }

  afterEach(() => {
    backend
      .match((r) => r.url === '/api/users/directory')
      .filter((r) => !r.cancelled)
      .forEach((r) => r.flush([]));
    backend.verify();
  });

  it('lists every team', async () => {
    const fixture = await render();

    expect(element(fixture).textContent).toContain('Press Office');
    expect(element(fixture).textContent).toContain('1 team');
  });

  it('deactivates a team after asking, with an idempotency key', async () => {
    const fixture = await render();

    element(fixture)
      .querySelector<HTMLButtonElement>('button[aria-label="Deactivate Press Office"]')!
      .click();
    await fixture.whenStable();
    expect(element(fixture).textContent).toContain('Deactivate Press Office?');

    button(fixture, 'Deactivate team').click();
    await fixture.whenStable();
    const request = backend.expectOne(
      (r) => r.url === '/api/teams/team-1' && r.method === 'DELETE',
    );
    expect(request.request.context.get(IDEMPOTENCY_KEY)).toEqual(expect.any(String));
    request.flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();
    backend.expectOne(meUrl).flush(adminUser);

    await vi.waitFor(async () => {
      await fixture.whenStable();
      expect(element(fixture).textContent).toContain('Create your first team');
    });
  });

  it('invites creating the first team when there are none', async () => {
    const fixture = await render([]);

    expect(element(fixture).textContent).toContain('Create your first team');
  });

  it('creates a team with an idempotency key and opens its members page', async () => {
    const fixture = await render();
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);

    button(fixture, 'New team').click();
    await fixture.whenStable();
    const name = element(fixture).querySelector<HTMLInputElement>('#new-team-name')!;
    const mailbox = element(fixture).querySelector<HTMLInputElement>('#new-team-mailbox')!;
    name.value = 'Benefits';
    name.dispatchEvent(new Event('input'));
    mailbox.value = 'benefits@example.com';
    mailbox.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    button(fixture, 'Create team').click();
    await fixture.whenStable();
    const request = backend.expectOne((r) => r.url === '/api/teams' && r.method === 'POST');

    expect(request.request.body).toEqual({
      name: 'Benefits',
      description: null,
      serviceAccountEmail: 'benefits@example.com',
      avatarColor: 'Mint',
    });
    expect(request.request.context.get(IDEMPOTENCY_KEY)).toEqual(expect.any(String));

    const created: Team = {
      id: 'team-2',
      name: 'Benefits',
      description: null,
      serviceAccountEmail: 'benefits@example.com',
      avatarColor: 'Mint',
      defaultSignature: null,
      confirmRecipientsAbove: 50,
      memberCount: 0,
      administratorCount: 0,
      dateCreated: '2026-11-23T10:00:00Z',
      dateModified: null,
      version: 'AAAAAAAAB9E=',
    };
    request.flush(created, { status: 201, statusText: 'Created' });
    await fixture.whenStable();
    backend.expectOne(meUrl).flush({
      ...adminUser,
      teams: [
        ...adminUser.teams,
        {
          id: 'team-2',
          name: 'Benefits',
          avatarColor: 'Mint',
          role: null,
          permissions: adminUser.permissions,
        },
      ],
    });
    await vi.waitFor(() => expect(navigate).toHaveBeenCalledWith('/team/members'));

    expect(element(fixture).textContent).toContain('Benefits');
  });

  it('shows a mailbox the directory does not know under its field', async () => {
    const fixture = await render();

    button(fixture, 'New team').click();
    await fixture.whenStable();
    const name = element(fixture).querySelector<HTMLInputElement>('#new-team-name')!;
    const mailbox = element(fixture).querySelector<HTMLInputElement>('#new-team-mailbox')!;
    name.value = 'Benefits';
    name.dispatchEvent(new Event('input'));
    mailbox.value = 'nobody@example.com';
    mailbox.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    button(fixture, 'Create team').click();
    await fixture.whenStable();
    backend
      .expectOne((r) => r.url === '/api/teams' && r.method === 'POST')
      .flush(
        { status: 404, detail: "No directory account has the service account's email." },
        { status: 404, statusText: 'Not Found' },
      );
    await vi.waitFor(async () => {
      await fixture.whenStable();
      expect(element(fixture).querySelector('#new-team-mailbox-error')?.textContent).toContain(
        'No directory account',
      );
    });
  });
});
