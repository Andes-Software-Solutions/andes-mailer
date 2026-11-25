import { HttpTestingController } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TeamMember } from '@core/api/team-member.model';
import {
  communicationsTeam,
  memberUser,
  provideSessionTesting,
  seedSession,
} from '@testing/session';
import { TeamMembers } from './team-members';

const membersUrl = `/api/teams/${communicationsTeam.id}/members`;

const self: TeamMember = {
  userId: memberUser.id,
  firstName: 'Maria',
  lastName: 'Lopez',
  fullName: 'Maria Lopez',
  email: 'maria.lopez@example.com',
  role: 'TeamAdministrator',
  dateCreated: '2026-11-01T10:00:00Z',
  dateModified: null,
  version: 'AAAAAAAAB9E=',
};

const priya: TeamMember = {
  ...self,
  userId: 'u-priya',
  firstName: 'Priya',
  lastName: 'Shah',
  fullName: 'Priya Shah',
  email: 'priya@example.com',
  role: 'TeamMember',
};

describe('TeamMembers', () => {
  let backend: HttpTestingController;

  async function render(members: readonly TeamMember[]): Promise<ComponentFixture<TeamMembers>> {
    TestBed.configureTestingModule({ providers: provideSessionTesting() });
    seedSession(memberUser, communicationsTeam.id);
    backend = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(TeamMembers);
    TestBed.tick();
    backend
      .expectOne((r) => r.url === membersUrl)
      .flush({ items: members, page: 1, pageSize: 100, totalCount: members.length });
    await fixture.whenStable();

    return fixture;
  }

  function element(fixture: ComponentFixture<TeamMembers>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  afterEach(() => backend.verify());

  it('lists the members with their roles and marks the signed-in person', async () => {
    const fixture = await render([self, priya]);
    const rows = element(fixture).querySelectorAll('tbody tr');

    expect(rows.length).toBe(2);
    expect(rows[0].textContent).toContain('Maria Lopez');
    expect(rows[0].textContent).toContain('(You)');
    expect(element(fixture).textContent).toContain('2 members · 1 administrator');
  });

  it('invites adding the first teammates to a team with no members', async () => {
    const fixture = await render([]);

    expect(element(fixture).textContent).toContain('Add your first teammates');
  });

  it('says so when a search matches nobody', async () => {
    const fixture = await render([self, priya]);
    const search = element(fixture).querySelector<HTMLInputElement>('input[type="search"]')!;

    search.value = 'rachel';
    search.dispatchEvent(new Event('input'));
    await fixture.whenStable();

    expect(element(fixture).textContent).toContain('No members match ‘rachel’');
  });

  it('explains the last-administrator rule instead of demoting the only administrator', async () => {
    const fixture = await render([self, priya]);
    const select = element(fixture).querySelector<HTMLSelectElement>(
      'select[aria-label="Role of Maria Lopez"]',
    )!;

    select.value = 'TeamMember';
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(element(fixture).textContent).toContain("You're the only administrator");
    expect(select.value).toBe('TeamAdministrator');
    backend.expectNone(`${membersUrl}/${self.userId}`);
  });

  it('asks before promoting someone to administrator', async () => {
    const fixture = await render([self, priya]);
    const select = element(fixture).querySelector<HTMLSelectElement>(
      'select[aria-label="Role of Priya Shah"]',
    )!;

    select.value = 'TeamAdministrator';
    select.dispatchEvent(new Event('change'));
    await fixture.whenStable();

    expect(element(fixture).textContent).toContain('Make Priya Shah an administrator?');
    backend.expectNone(`${membersUrl}/${priya.userId}`);
  });

  it('asks before removing a member', async () => {
    const fixture = await render([self, priya]);

    element(fixture)
      .querySelector<HTMLButtonElement>('button[aria-label="Remove Priya Shah"]')!
      .click();
    await fixture.whenStable();

    expect(element(fixture).textContent).toContain(
      'Remove Priya Shah from Corporate Communications?',
    );
  });
});
