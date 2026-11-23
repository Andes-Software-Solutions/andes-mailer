import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { THEME_STORAGE_KEY } from './theme-preference';
import { ThemeStore } from './theme-store';

/** jsdom has no `matchMedia`; this one lets a spec flip the OS theme. */
function fakeMatchMedia(prefersDark: boolean): { setDark(dark: boolean): void } {
  const listeners = new Set<(event: MediaQueryListEvent) => void>();
  const list = {
    matches: prefersDark,
    addEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) =>
      listeners.add(listener),
    removeEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) =>
      listeners.delete(listener),
  };

  vi.stubGlobal('matchMedia', () => list);

  return {
    setDark(dark: boolean) {
      list.matches = dark;
      listeners.forEach((listener) => listener({ matches: dark } as MediaQueryListEvent));
    },
  };
}

describe('ThemeStore', () => {
  function theme(): string | null {
    return document.documentElement.getAttribute('data-bs-theme');
  }

  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-bs-theme');
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('follows the OS when nothing was picked', () => {
    const os = fakeMatchMedia(true);
    const store = TestBed.inject(ThemeStore);
    TestBed.tick();

    expect(store.preference()).toBe('system');
    expect(theme()).toBe('dark');

    os.setDark(false);
    TestBed.tick();

    expect(theme()).toBe('light');
  });

  it('starts from the stored choice', () => {
    fakeMatchMedia(false);
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');

    TestBed.inject(ThemeStore);
    TestBed.tick();

    expect(theme()).toBe('dark');
  });

  it('turns a toggle into an explicit, stored choice', () => {
    fakeMatchMedia(false);
    const store = TestBed.inject(ThemeStore);

    store.toggle();
    TestBed.tick();

    expect(store.preference()).toBe('dark');
    expect(theme()).toBe('dark');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('dark');
  });

  it('forgets the stored choice when set back to the system', () => {
    fakeMatchMedia(false);
    const store = TestBed.inject(ThemeStore);
    store.setPreference('dark');

    store.setPreference('system');
    TestBed.tick();

    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBeNull();
    expect(theme()).toBe('light');
  });
});
