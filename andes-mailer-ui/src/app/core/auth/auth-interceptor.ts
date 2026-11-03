import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { from, switchMap } from 'rxjs';
import { Auth } from './auth';

/** Only our API gets the bearer token, never `config.json` or a third-party host. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/')) {
    return next(request);
  }

  return from(inject(Auth).getAccessToken()).pipe(
    switchMap((token) =>
      next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })),
    ),
  );
};
