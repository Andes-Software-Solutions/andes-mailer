import { HttpErrorResponse, HttpInterceptorFn, HttpStatusCode } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { Auth } from './auth';

/** Only our API gets the bearer token, never `config.json` or a third-party host. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/')) {
    return next(request);
  }

  const auth = inject(Auth);
  const send = (refresh: boolean) =>
    from(auth.getAccessToken({ refresh })).pipe(
      switchMap((token) =>
        next(request.clone({ setHeaders: { Authorization: `Bearer ${token}` } })),
      ),
    );

  // The API can reject a token MSAL still trusts, such as when this PC's clock is behind, so a
  // 401 gets one retry with a fresh token. A 403 never does: a new token carries the same scopes.
  return send(false).pipe(
    catchError((error: unknown) =>
      error instanceof HttpErrorResponse && error.status === HttpStatusCode.Unauthorized
        ? send(true)
        : throwError(() => error),
    ),
  );
};
