import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';

export type ToastTone = 'ok' | 'error';

export interface ToastAction {
  readonly label: string;
  readonly run: () => void;
}

export interface Toast {
  readonly id: number;
  readonly message: string;
  readonly tone: ToastTone;
  readonly action: ToastAction | null;
}

export interface ToastRequest {
  readonly message: string;
  readonly tone?: ToastTone;
  readonly action?: ToastAction;
}

interface ToastState {
  toasts: readonly Toast[];
}

/** Long enough to read and reach an Undo; a toast with an action stays a little longer. */
export const TOAST_DURATION_MS = 5000;
export const TOAST_ACTION_DURATION_MS = 8000;

/** The most toasts on screen; an older one gives way to a new one. */
const MAX_TOASTS = 3;

/** Short confirmations after an action, shown by the shell's toast stack. */
export const ToastStore = signalStore(
  { providedIn: 'root' },
  withState<ToastState>({ toasts: [] }),
  withMethods((store) => {
    let lastId = 0;
    const timers = new Map<number, ReturnType<typeof setTimeout>>();

    function dismiss(id: number): void {
      clearTimeout(timers.get(id));
      timers.delete(id);
      patchState(store, (state) => ({ toasts: state.toasts.filter((toast) => toast.id !== id) }));
    }

    return {
      show(request: ToastRequest): number {
        const toast: Toast = {
          id: ++lastId,
          message: request.message,
          tone: request.tone ?? 'ok',
          action: request.action ?? null,
        };
        const overflow = store
          .toasts()
          .slice(0, Math.max(0, store.toasts().length - MAX_TOASTS + 1));

        overflow.forEach((old) => dismiss(old.id));
        patchState(store, (state) => ({ toasts: [...state.toasts, toast] }));
        timers.set(
          toast.id,
          setTimeout(
            () => dismiss(toast.id),
            toast.action ? TOAST_ACTION_DURATION_MS : TOAST_DURATION_MS,
          ),
        );

        return toast.id;
      },

      dismiss,

      /** Runs the toast's action once and closes it. */
      act(id: number): void {
        const toast = store.toasts().find((candidate) => candidate.id === id);

        dismiss(id);
        toast?.action?.run();
      },
    };
  }),
);
