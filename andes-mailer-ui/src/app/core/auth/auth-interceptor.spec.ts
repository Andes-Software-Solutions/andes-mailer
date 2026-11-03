import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideTestAppConfig } from '@testing/app-config';
import { provideFakeMsal, signedInMsal } from '@testing/msal';
import { firstValueFrom } from 'rxjs';
import { vi } from 'vitest';
import { authInterceptor } from './auth-interceptor';

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideTestAppConfig(),
        provideFakeMsal(signedInMsal()),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });

    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    backend.verify();
  });

  it('adds the bearer token to API requests', async () => {
    const response = firstValueFrom(http.get('/api/users/me'));

    const request = await vi.waitFor(() => backend.expectOne('/api/users/me'));
    request.flush({});
    await response;

    expect(request.request.headers.get('Authorization')).toBe('Bearer access-token');
  });

  it.each(['config.json', '/apiary', 'https://graph.microsoft.com/v1.0/me'])(
    'leaves %s without a token',
    (url) => {
      http.get(url).subscribe();

      const request = backend.expectOne(url);
      request.flush({});

      expect(request.request.headers.has('Authorization')).toBe(false);
    },
  );
});
