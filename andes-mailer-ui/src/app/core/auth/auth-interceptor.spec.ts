import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MsalBroadcastService } from '@azure/msal-angular';
import {
  IPublicClientApplication,
  InteractionRequiredAuthError,
  InteractionStatus,
} from '@azure/msal-browser';
import { provideTestAppConfig, testApiScope } from '@testing/app-config';
import { fakeAuthResult, provideFakeMsal, signedInMsal } from '@testing/msal';
import { filter, firstValueFrom } from 'rxjs';
import { vi } from 'vitest';
import { authInterceptor } from './auth-interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  async function setup(instance: IPublicClientApplication = signedInMsal()): Promise<void> {
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

    // The app initializer's redirect handling has finished, as it has by any user's first request.
    await firstValueFrom(
      TestBed.inject(MsalBroadcastService).inProgress$.pipe(
        filter((status) => status === InteractionStatus.None),
      ),
    );
  }

  afterEach(() => {
    backend.verify();
  });

  it('adds the bearer token for the API scope to API requests', async () => {
    const acquireTokenSilent = vi.fn<IPublicClientApplication['acquireTokenSilent']>(() =>
      Promise.resolve(fakeAuthResult()),
    );
    await setup(signedInMsal({ acquireTokenSilent }));

    const response = firstValueFrom(http.get('/api/users/me'));
    const request = await vi.waitFor(() => backend.expectOne('/api/users/me'));
    request.flush({});
    await response;

    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
    expect(acquireTokenSilent).toHaveBeenCalledWith(
      expect.objectContaining({ scopes: [testApiScope] }),
    );
  });

  it.each(['/config.json', '/apiary', 'https://graph.microsoft.com/v1.0/me'])(
    'never sends the token to %s',
    async (url) => {
      const acquireTokenSilent = vi.fn(() => Promise.resolve(fakeAuthResult()));
      await setup(signedInMsal({ acquireTokenSilent }));

      const response = firstValueFrom(http.get(url));
      const request = backend.expectOne(url);
      request.flush({});
      await response;

      expect(request.request.headers.has('Authorization')).toBe(false);
      expect(acquireTokenSilent).not.toHaveBeenCalled();
    },
  );

  it('redirects to Entra when no token can be acquired silently', async () => {
    const acquireTokenRedirect = vi.fn<IPublicClientApplication['acquireTokenRedirect']>(() =>
      Promise.resolve(),
    );
    await setup(
      signedInMsal({
        acquireTokenSilent: () =>
          Promise.reject(new InteractionRequiredAuthError('interaction_required', '')),
        acquireTokenRedirect,
      }),
    );

    http.get('/api/users/me').subscribe();

    await vi.waitFor(() =>
      expect(acquireTokenRedirect).toHaveBeenCalledWith(
        expect.objectContaining({ scopes: [testApiScope] }),
      ),
    );
    backend.expectNone('/api/users/me');
  });
});
