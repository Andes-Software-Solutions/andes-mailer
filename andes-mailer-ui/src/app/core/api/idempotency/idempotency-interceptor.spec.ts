import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { vi } from 'vitest';
import { idempotencyInterceptor } from './idempotency-interceptor';
import { IDEMPOTENCY_KEY_HEADER, idempotent } from './idempotency-key';

describe('idempotencyInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    vi.useFakeTimers();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([idempotencyInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    backend.verify();
    vi.useRealTimers();
  });

  it('sends the key of a keyed write', async () => {
    const response = firstValueFrom(http.post('/api/teams', {}, { context: idempotent('key-1') }));

    const request = backend.expectOne('/api/teams');
    request.flush({ id: 'team' });

    expect(request.request.headers.get(IDEMPOTENCY_KEY_HEADER)).toBe('key-1');
    await expect(response).resolves.toEqual({ id: 'team' });
  });

  it('leaves a request without a key untouched and never retries it', async () => {
    const response = firstValueFrom(http.post('/api/teams', {}));

    const request = backend.expectOne('/api/teams');
    request.error(new ProgressEvent('error'), { status: 0 });

    expect(request.request.headers.has(IDEMPOTENCY_KEY_HEADER)).toBe(false);
    await expect(response).rejects.toMatchObject({ status: 0 });
  });

  it('retries a lost response with the same key', async () => {
    const response = firstValueFrom(http.post('/api/teams', {}, { context: idempotent('key-1') }));

    backend.expectOne('/api/teams').error(new ProgressEvent('error'), { status: 0 });
    await vi.runOnlyPendingTimersAsync();
    const retry = backend.expectOne('/api/teams');
    retry.flush({ id: 'team' }, { headers: { 'Idempotent-Replayed': 'true' } });

    expect(retry.request.headers.get(IDEMPOTENCY_KEY_HEADER)).toBe('key-1');
    await expect(response).resolves.toEqual({ id: 'team' });
  });

  it('retries while the first request with the key is still running', async () => {
    const response = firstValueFrom(http.delete('/api/teams/1', { context: idempotent('key-1') }));

    backend
      .expectOne('/api/teams/1')
      .flush(null, { status: 409, statusText: 'Conflict', headers: { 'Retry-After': '1' } });
    await vi.advanceTimersByTimeAsync(1000);
    backend.expectOne('/api/teams/1').flush(null, { status: 204, statusText: 'No Content' });

    await expect(response).resolves.toBeNull();
  });

  it('gives up after the retries run out', async () => {
    const response = firstValueFrom(http.post('/api/teams', {}, { context: idempotent('key-1') }));
    const settled = response.catch((error: unknown) => error);

    for (let attempt = 0; attempt < 3; attempt++) {
      backend.expectOne('/api/teams').error(new ProgressEvent('error'), { status: 0 });
      await vi.runOnlyPendingTimersAsync();
    }

    await expect(settled).resolves.toMatchObject({ status: 0 });
  });

  it('does not retry an answer the API definitely gave', async () => {
    const response = firstValueFrom(http.post('/api/teams', {}, { context: idempotent('key-1') }));

    backend.expectOne('/api/teams').flush(null, { status: 409, statusText: 'Conflict' });

    await expect(response).rejects.toMatchObject({ status: 409 });
  });
});
