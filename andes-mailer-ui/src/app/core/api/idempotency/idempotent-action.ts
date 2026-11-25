import { isTransient } from './idempotency-interceptor';
import { newIdempotencyKey } from './idempotency-key';

/**
 * One user intent, such as "save these settings", and the `Idempotency-Key` it is sent with.
 *
 * The key stays the same while the person retries the same payload after a failure that may not
 * have reached the API, so pressing Save again after a dropped connection replays the first result
 * instead of saving twice. A success, an answer the API definitely gave (any other error), or a
 * changed payload starts a new key.
 */
export class IdempotentAction {
  private key: string | null = null;
  private fingerprint: string | null = null;

  /** The key for sending `payload` now. */
  keyFor(payload: unknown): string {
    const fingerprint = JSON.stringify(payload ?? null);

    if (this.key === null || this.fingerprint !== fingerprint) {
      this.key = newIdempotencyKey();
      this.fingerprint = fingerprint;
    }

    return this.key;
  }

  /** Call when the request finished; pass the error if it failed. */
  settle(error?: unknown): void {
    if (error !== undefined && isTransient(error)) {
      return;
    }

    this.key = null;
    this.fingerprint = null;
  }
}
