import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { retry, timer } from 'rxjs';
import { IDEMPOTENCY_KEY, IDEMPOTENCY_KEY_HEADER } from './idempotency-key';

/** How many times a keyed write is sent again after a transient failure. */
export const IDEMPOTENT_RETRIES = 2;

const RETRY_BASE_DELAY_MS = 500;

/**
 * Sends a write's `Idempotency-Key` and retries it with the same key when the failure may not have
 * reached the API, or reached it and was lost on the way back. The API answers a repeat with the
 * first response, so a retry never applies a change twice. Requests without a key pass untouched,
 * because only a keyed write is safe to repeat.
 */
export const idempotencyInterceptor: HttpInterceptorFn = (request, next) => {
  const key = request.context.get(IDEMPOTENCY_KEY);

  if (key === null) {
    return next(request);
  }

  return next(request.clone({ setHeaders: { [IDEMPOTENCY_KEY_HEADER]: key } })).pipe(
    retry({
      count: IDEMPOTENT_RETRIES,
      delay: (error: unknown, attempt) => {
        if (!isTransient(error)) {
          throw error;
        }

        return timer(retryAfterMs(error) ?? RETRY_BASE_DELAY_MS * 2 ** (attempt - 1));
      },
    }),
  );
};

/**
 * No response (0), a timeout, a gateway or availability failure, or the API reporting that the
 * first request with this key is still running (409 with `Retry-After`).
 */
export function isTransient(error: unknown): error is HttpErrorResponse {
  if (!(error instanceof HttpErrorResponse)) {
    return false;
  }

  switch (error.status) {
    case 0:
    case 408:
    case 502:
    case 503:
    case 504:
      return true;
    case 409:
      return error.headers.has('Retry-After');
    default:
      return false;
  }
}

function retryAfterMs(error: HttpErrorResponse): number | null {
  const seconds = Number(error.headers.get('Retry-After'));

  return Number.isFinite(seconds) && seconds > 0 ? seconds * 1000 : null;
}
