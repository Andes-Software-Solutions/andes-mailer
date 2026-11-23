import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { IPublicClientApplication } from '@azure/msal-browser';
import { provideTestAppConfig } from '@testing/app-config';
import { provideFakeMsal, signedInMsal } from '@testing/msal';
import { adminUser, communicationsTeam, memberUser, seedSession } from '@testing/session';
import { vi } from 'vitest';
import { routes } from '../../app.routes';
import { Shell } from './shell';

describe('Shell', () => {
  async function open(
    url: string,
    seed: () => void,
    instance: IPublicClientApplication = signedInMsal(),
  ): Promise<RouterTestingHarness> {
    TestBed.configureTestingModule({
      providers: [
        provideTestAppConfig(),
        provideFakeMsal(instance),
        provideRouter(routes, withComponentInputBinding()),
        provideHttpClient(),
        provideHttpClientTesting(),
      ],
    });
    const harness = await RouterTestingHarness.create();
    seed();
    await harness.navigateByUrl(url, Shell);

    return harness;
  }

  function sidebar(harness: RouterTestingHarness): HTMLElement {
    return harness.routeNativeElement?.querySelector('aside') as HTMLElement;
  }

  function navLabels(harness: RouterTestingHarness): string[] {
    return [...sidebar(harness).querySelectorAll('nav[aria-label=Main] .nav-text')].map(
      (element) => element.textContent?.trim() ?? '',
    );
  }

  /** Bootstrap finishes showing a beat later and then focuses the panel; act only after that. */
  async function menuShown(root: HTMLElement): Promise<void> {
    await vi.waitFor(() => expect(root.querySelector('.offcanvas.show')).not.toBeNull());
  }

  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('shows a member the main nav without the administration section', async () => {
    const harness = await open('/', () => seedSession(memberUser));

    expect(navLabels(harness)).toEqual([
      'Compose',
      'Drafts',
      'Scheduled',
      'Sent',
      'Templates',
      'Email groups',
    ]);
    expect(sidebar(harness).textContent).not.toContain('Administration');
  });

  it('shows an administrator the administration section', async () => {
    const harness = await open('/', () => seedSession(adminUser));

    expect(navLabels(harness)).toContain('Team members');
    expect(navLabels(harness)).toContain('Team settings');
    expect(sidebar(harness).textContent).toContain('Administration');
  });

  it('follows a team switch in the nav', async () => {
    const harness = await open('/', () => seedSession(memberUser));

    const item = [
      ...sidebar(harness).querySelectorAll<HTMLButtonElement>('[role=menuitemradio]'),
    ].find((button) => button.textContent?.includes(communicationsTeam.name));
    item?.click();
    await harness.fixture.whenStable();

    expect(navLabels(harness)).toContain('Team members');
    expect(item?.getAttribute('aria-checked')).toBe('true');
  });

  it('returns focus to the team switcher after a team is picked', async () => {
    const harness = await open('/', () => seedSession(memberUser));
    const toggle = sidebar(harness).querySelector<HTMLButtonElement>('.team-switch');

    toggle?.click();
    await harness.fixture.whenStable();
    const item = [
      ...sidebar(harness).querySelectorAll<HTMLButtonElement>('[role=menuitemradio]'),
    ].find((button) => button.textContent?.includes(communicationsTeam.name));
    item?.focus();
    item?.click();

    await vi.waitFor(() => expect(document.activeElement).toBe(toggle));
  });

  it('moves focus to the new page after a menu link is followed', async () => {
    const harness = await open('/', () => seedSession(memberUser));
    const root = harness.routeNativeElement as HTMLElement;
    const main = root.querySelector('main');

    root.querySelector<HTMLButtonElement>('[aria-label="Open menu"]')?.click();
    await menuShown(root);
    const drafts = [...root.querySelectorAll<HTMLAnchorElement>('.offcanvas .nav-item')].find(
      (link) => link.textContent?.includes('Drafts'),
    );
    drafts?.focus();
    drafts?.click();

    await vi.waitFor(async () => {
      await harness.fixture.whenStable();
      expect(TestBed.inject(Router).url).toBe('/drafts');
      expect(document.activeElement).toBe(main);
    });

    // In a browser the slide-out ends ~300 ms later, after the page has focus; replay that order.
    root.querySelector('.offcanvas')?.dispatchEvent(new Event('hidden.bs.offcanvas'));
    expect(document.activeElement).toBe(main);
  });

  it('returns focus to the menu button when the menu is closed', async () => {
    const harness = await open('/', () => seedSession(memberUser));
    const root = harness.routeNativeElement as HTMLElement;
    const opener = root.querySelector<HTMLButtonElement>('[aria-label="Open menu"]');

    opener?.click();
    await menuShown(root);
    const close = root.querySelector<HTMLButtonElement>('.offcanvas [aria-label="Close menu"]');
    close?.focus();
    close?.click();

    await vi.waitFor(() => expect(document.activeElement).toBe(opener));
  });

  it('leaves a page the newly picked team does not permit', async () => {
    const harness = await open('/team/members', () =>
      seedSession(memberUser, communicationsTeam.id),
    );
    const router = TestBed.inject(Router);
    expect(router.url).toBe('/team/members');

    const hr = [
      ...sidebar(harness).querySelectorAll<HTMLButtonElement>('[role=menuitemradio]'),
    ].find((button) => button.textContent?.includes('HR Announcements'));
    hr?.click();
    await vi.waitFor(async () => {
      await harness.fixture.whenStable();
      expect(router.url).toBe('/');
    });
  });

  it('names the page in the top bar', async () => {
    const harness = await open('/drafts', () => seedSession(memberUser));

    expect(harness.routeNativeElement?.querySelector('.crumbs')?.textContent?.trim()).toBe(
      'Drafts',
    );
  });
});
