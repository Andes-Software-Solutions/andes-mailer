import { HttpErrorResponse } from '@angular/common/http';
import { IdempotentAction } from './idempotent-action';

describe('IdempotentAction', () => {
  const lost = new HttpErrorResponse({ status: 0 });
  const rejected = new HttpErrorResponse({ status: 400 });

  it('keeps the key while the same payload is retried after a lost response', () => {
    const action = new IdempotentAction();
    const first = action.keyFor({ name: 'Sales' });

    action.settle(lost);

    expect(action.keyFor({ name: 'Sales' })).toBe(first);
  });

  it('starts a new key once the API answered', () => {
    const action = new IdempotentAction();
    const first = action.keyFor({ name: 'Sales' });

    action.settle(rejected);

    expect(action.keyFor({ name: 'Sales' })).not.toBe(first);
  });

  it('starts a new key after a success', () => {
    const action = new IdempotentAction();
    const first = action.keyFor({ name: 'Sales' });

    action.settle();

    expect(action.keyFor({ name: 'Sales' })).not.toBe(first);
  });

  it('starts a new key when the payload changes', () => {
    const action = new IdempotentAction();
    const first = action.keyFor({ name: 'Sales' });

    action.settle(lost);

    expect(action.keyFor({ name: 'Support' })).not.toBe(first);
  });
});
