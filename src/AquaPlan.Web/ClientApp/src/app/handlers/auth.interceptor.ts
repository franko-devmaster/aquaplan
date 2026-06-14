import { HttpInterceptorFn } from '@angular/common/http';
import { inject, Injector } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { catchError, throwError } from 'rxjs';

/**
 * AQ-409 — offline-aware auth interceptor.
 * F-036 — the token is read through AuthService.getToken() (signal-backed single
 * source of truth, mirrored to sessionStorage / IndexedDB) instead of reading the
 * raw sessionStorage key directly, so the interceptor can never diverge from the
 * service's storage strategy. On a 401, delegate to AuthService.handleAuthFailure()
 * which decides to logout only when the device is online — keeping field sessions
 * alive when connectivity drops.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const injector = inject(Injector);
  const authService = injector.get(AuthService);

  const token = authService.getToken();
  if (token) {
    req = req.clone({
      setHeaders: {
        Authorization: `Bearer ${token}`,
      },
    });
  }

  return next(req).pipe(
    catchError((error) => {
      if (error.status === 401 && !req.url.includes('/api/auth/login')) {
        void authService.handleAuthFailure();
      }
      return throwError(() => error);
    })
  );
};
