import { ComponentFixture, TestBed } from '@angular/core/testing';
import { CurrentUser } from '@core/api/current-user.model';
import { memberUser, noTeamUser, provideSessionTesting, seedSession } from '@testing/session';
import { Home } from './home';

describe('Home', () => {
  async function render(user: CurrentUser): Promise<ComponentFixture<Home>> {
    TestBed.configureTestingModule({ providers: provideSessionTesting() });
    seedSession(user);

    const fixture = TestBed.createComponent(Home);
    await fixture.whenStable();

    return fixture;
  }

  function element(fixture: ComponentFixture<Home>): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  beforeEach(() => {
    localStorage.clear();
  });

  it('welcomes the user by first name', async () => {
    const fixture = await render(memberUser);

    expect(element(fixture).querySelector('h1')?.textContent?.trim()).toBe('Welcome, Maria');
  });

  it('names the team the user is working in', async () => {
    const fixture = await render(memberUser);

    expect(element(fixture).textContent).toContain('You are working in HR Announcements.');
  });

  it('says so when the user is on no team', async () => {
    const fixture = await render(noTeamUser);

    expect(element(fixture).textContent).toContain("You're not on a team yet");
  });
});
