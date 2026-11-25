import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { TOAST_ACTION_DURATION_MS, TOAST_DURATION_MS, ToastStore } from './toast-store';

describe('ToastStore', () => {
  let store: InstanceType<typeof ToastStore>;

  beforeEach(() => {
    vi.useFakeTimers();
    store = TestBed.inject(ToastStore);
  });

  afterEach(() => vi.useRealTimers());

  it('dismisses a toast on its own after a while', () => {
    store.show({ message: 'Role updated' });

    vi.advanceTimersByTime(TOAST_DURATION_MS - 1);
    expect(store.toasts().length).toBe(1);

    vi.advanceTimersByTime(1);
    expect(store.toasts()).toEqual([]);
  });

  it('keeps a toast with an action longer', () => {
    store.show({ message: 'Priya Shah removed', action: { label: 'Undo', run: () => undefined } });

    vi.advanceTimersByTime(TOAST_DURATION_MS);
    expect(store.toasts().length).toBe(1);

    vi.advanceTimersByTime(TOAST_ACTION_DURATION_MS - TOAST_DURATION_MS);
    expect(store.toasts()).toEqual([]);
  });

  it('shows at most three, dropping the oldest', () => {
    ['one', 'two', 'three', 'four'].forEach((message) => store.show({ message }));

    expect(store.toasts().map((toast) => toast.message)).toEqual(['two', 'three', 'four']);
  });

  it('runs the action once and closes the toast', () => {
    const run = vi.fn();
    const id = store.show({ message: 'Priya Shah removed', action: { label: 'Undo', run } });

    store.act(id);
    store.act(id);

    expect(run).toHaveBeenCalledTimes(1);
    expect(store.toasts()).toEqual([]);
  });
});
