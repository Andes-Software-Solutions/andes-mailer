import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MsalInterceptor } from '@azure/msal-angular';

/**
 * `MsalInterceptor` behind a functional interceptor, so `withInterceptors` can run it instead of
 * the DI-based chain Angular may phase out. Its `protectedResourceMap` decides which requests get a
 * token.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) =>
  inject(MsalInterceptor).intercept(request, { handle: next });
