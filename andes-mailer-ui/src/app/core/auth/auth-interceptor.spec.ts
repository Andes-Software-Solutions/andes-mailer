import {
  HttpClient,
  HttpErrorResponse,
  provideHttpClient,
  withInterceptors,
} from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { CacheLookupPolicy, IPublicClientApplication, SilentRequest } from '@azure/msal-browser';
import { provideTestAppConfig } from '@testing/app-config';
import { fakeAuthResult, provideFakeMsal, signedInMsal } from '@testing/msal';
import { firstValueFrom } from 'rxjs';
import { vi } from 'vitest';
import { authInterceptor } from './auth-interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  function setup(instance: IPublicClientApplication = signedInMsal()): void {
    TestBed.configureTestingModule({
      providers: [
        provideTestAppConfig(),
        provideFakeMsal(instance),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  }

  /** Hands out `stale` from the cache and `fresh` once the cache is skipped. */
  function msalWithStaleCache(): IPublicClientApplication {
    return signedInMsal({
      acquireTokenSilent: (request: SilentRequest) =>
        Promise.resolve(
          fakeAuthResult({
            accessToken:
              request.cacheLookupPolicy === CacheLookupPolicy.RefreshTokenAndNetwork
                ? 'fresh'
                : 'stale',
          }),
        ),
    });
  }

  function nextRequest(): Promise<ReturnType<HttpTestingController['expectOne']>> {
    return vi.waitFor(() => backend.expectOne('/api/users/me'));
  }

  afterEach(() => {
    backend.verify();
  });

  it('adds the bearer token to API requests', async () => {
    setup();
    const response = firstValueFrom(http.get('/api/users/me'));

    const request = await nextRequest();
    request.flush({});
    await response;

    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
  });

  it.each(['config.json', '/apiary', 'https://graph.microsoft.com/v1.0/me'])(
    'leaves %s without a token',
    (url) => {
      setup();
      http.get(url).subscribe();

      const request = backend.expectOne(url);
      request.flush({});

      expect(request.request.headers.has('Authorization')).toBe(false);
    },
  );

  it('retries a rejected token once with a fresh one', async () => {
    setup(msalWithStaleCache());
    const response = firstValueFrom(http.get('/api/users/me'));

    const first = await nextRequest();
    first.flush(null, { status: 401, statusText: 'Unauthorized' });
    const second = await nextRequest();
    second.flush({ name: 'Ada Lovelace' });

    await expect(response).resolves.toEqual({ name: 'Ada Lovelace' });
    expect(first.request.headers.get('Authorization')).toBe('Bearer stale');
    expect(second.request.headers.get('Authorization')).toBe('Bearer fresh');
  });

  it('returns a second 401 to the caller instead of retrying again', async () => {
    setup(msalWithStaleCache());
    const response = firstValueFrom(http.get('/api/users/me'));

    (await nextRequest()).flush(null, { status: 401, statusText: 'Unauthorized' });
    (await nextRequest()).flush(null, { status: 401, statusText: 'Unauthorized' });

    const error: unknown = await response.catch((cause: unknown) => cause);
    expect(error).toBeInstanceOf(HttpErrorResponse);
    expect((error as HttpErrorResponse).status).toBe(401);
  });

  it('never retries a 403, which a fresh token would not change', async () => {
    setup(msalWithStaleCache());
    const response = firstValueFrom(http.get('/api/users/me'));

    (await nextRequest()).flush(null, { status: 403, statusText: 'Forbidden' });

    const error: unknown = await response.catch((cause: unknown) => cause);
    expect((error as HttpErrorResponse).status).toBe(403);
  });
});
