import { HttpContext, HttpContextToken } from '@angular/common/http';

/** The `Idempotency-Key` a write request carries; `null` sends none. */
export const IDEMPOTENCY_KEY = new HttpContextToken<string | null>(() => null);

export const IDEMPOTENCY_KEY_HEADER = 'Idempotency-Key';

/** Set by the API when it answered a repeated key with the first response instead of applying the change again. */
export const IDEMPOTENT_REPLAYED_HEADER = 'Idempotent-Replayed';

export function newIdempotencyKey(): string {
  return crypto.randomUUID();
}

/** The request context that makes `idempotencyInterceptor` send `key`. */
export function idempotent(key: string): HttpContext {
  return new HttpContext().set(IDEMPOTENCY_KEY, key);
}
