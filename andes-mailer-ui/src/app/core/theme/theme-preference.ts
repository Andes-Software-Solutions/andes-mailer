/** What the person picked; `system` follows the operating system. */
export type ThemePreference = 'light' | 'dark' | 'system';

/** The theme Bootstrap renders, set as `data-bs-theme` on `<html>`. */
export type Theme = 'light' | 'dark';

export const THEME_STORAGE_KEY = 'andes-mailer.theme';

export const PREFERS_DARK_QUERY = '(prefers-color-scheme: dark)';

/** Anything but an explicit choice reads as `system`, so a cleared or blocked store follows the OS. */
export function readThemePreference(
  storage: Pick<Storage, 'getItem'> | undefined,
): ThemePreference {
  try {
    const stored = storage?.getItem(THEME_STORAGE_KEY);

    return stored === 'light' || stored === 'dark' ? stored : 'system';
  } catch {
    return 'system';
  }
}

/** Keeps only an explicit choice, so a person who never picks one keeps following the OS. */
export function writeThemePreference(
  storage: Pick<Storage, 'setItem' | 'removeItem'> | undefined,
  preference: ThemePreference,
): void {
  try {
    if (preference === 'system') {
      storage?.removeItem(THEME_STORAGE_KEY);
    } else {
      storage?.setItem(THEME_STORAGE_KEY, preference);
    }
  } catch {
    // Storage blocked (private mode, policy): the choice lasts for this page only.
  }
}

export function resolveTheme(preference: ThemePreference, prefersDark: boolean): Theme {
  if (preference === 'system') {
    return prefersDark ? 'dark' : 'light';
  }

  return preference;
}

export function applyTheme(root: HTMLElement, theme: Theme): void {
  root.setAttribute('data-bs-theme', theme);
}

/**
 * Runs before Angular boots, so the first paint, the sign-in splash included, is already in the
 * right theme.
 */
export function applyInitialTheme(document: Document): void {
  const window = document.defaultView;
  const preference = readThemePreference(safeLocalStorage(window));
  const prefersDark = window?.matchMedia?.(PREFERS_DARK_QUERY).matches ?? false;

  applyTheme(document.documentElement, resolveTheme(preference, prefersDark));
}

/** Reading `localStorage` itself throws where storage is blocked. */
export function safeLocalStorage(window: Window | null | undefined): Storage | undefined {
  try {
    return window?.localStorage;
  } catch {
    return undefined;
  }
}
