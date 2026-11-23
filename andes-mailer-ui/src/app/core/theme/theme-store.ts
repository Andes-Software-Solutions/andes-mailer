import { DOCUMENT } from '@angular/common';
import { DestroyRef, effect, inject, signal } from '@angular/core';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withProps,
  withState,
} from '@ngrx/signals';
import {
  PREFERS_DARK_QUERY,
  ThemePreference,
  applyTheme,
  readThemePreference,
  resolveTheme,
  safeLocalStorage,
  writeThemePreference,
} from './theme-preference';

interface ThemeState {
  preference: ThemePreference;
}

/** Light or dark on `<html>`: the person's explicit choice, otherwise the operating system's. */
export const ThemeStore = signalStore(
  { providedIn: 'root' },
  withProps(() => {
    const document = inject(DOCUMENT);
    const media = document.defaultView?.matchMedia?.(PREFERS_DARK_QUERY);
    const prefersDark = signal(media?.matches ?? false);
    const onChange = (event: MediaQueryListEvent): void => prefersDark.set(event.matches);

    media?.addEventListener('change', onChange);
    inject(DestroyRef).onDestroy(() => media?.removeEventListener('change', onChange));

    return {
      _root: document.documentElement,
      _storage: safeLocalStorage(document.defaultView),
      _prefersDark: prefersDark.asReadonly(),
    };
  }),
  withState<ThemeState>({ preference: 'system' }),
  withComputed(({ preference, _prefersDark }) => ({
    resolved: () => resolveTheme(preference(), _prefersDark()),
  })),
  withMethods((store) => ({
    setPreference(preference: ThemePreference): void {
      patchState(store, { preference });
      writeThemePreference(store._storage, preference);
    },
  })),
  withMethods((store) => ({
    /** Turns the theme on screen into an explicit choice of the other one. */
    toggle(): void {
      store.setPreference(store.resolved() === 'dark' ? 'light' : 'dark');
    },
  })),
  withHooks({
    onInit(store) {
      patchState(store, { preference: readThemePreference(store._storage) });
      effect(() => applyTheme(store._root, store.resolved()));
    },
  }),
);
