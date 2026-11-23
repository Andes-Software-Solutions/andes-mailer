import {
  THEME_STORAGE_KEY,
  applyInitialTheme,
  readThemePreference,
  resolveTheme,
  writeThemePreference,
} from './theme-preference';

describe('theme preference', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-bs-theme');
  });

  it('reads an explicit choice', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');

    expect(readThemePreference(localStorage)).toBe('dark');
  });

  it('reads anything else as following the system', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'purple');

    expect(readThemePreference(localStorage)).toBe('system');
    expect(readThemePreference(undefined)).toBe('system');
  });

  it('reads a blocked store as following the system', () => {
    const blocked = {
      getItem: () => {
        throw new Error('SecurityError');
      },
    };

    expect(readThemePreference(blocked)).toBe('system');
  });

  it('keeps an explicit choice and forgets the system one', () => {
    writeThemePreference(localStorage, 'light');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light');

    writeThemePreference(localStorage, 'system');
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBeNull();
  });

  it('resolves the system choice from the OS', () => {
    expect(resolveTheme('system', true)).toBe('dark');
    expect(resolveTheme('system', false)).toBe('light');
    expect(resolveTheme('light', true)).toBe('light');
  });

  it('applies the stored theme before the app boots', () => {
    localStorage.setItem(THEME_STORAGE_KEY, 'dark');

    applyInitialTheme(document);

    expect(document.documentElement.getAttribute('data-bs-theme')).toBe('dark');
  });
});
