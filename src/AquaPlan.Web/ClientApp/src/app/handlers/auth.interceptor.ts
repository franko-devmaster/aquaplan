import { HttpInterceptorFn } from '@angular/common/http';
import { inject, Injector } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { catchError, throwError } from 'rxjs';

/**
 * AQ-409 — offline-aware auth interceptor.
 * The token is read from sessionStorage for speed (AuthService mirrors it there on
 * every login / IDB restore). On a 401, delegate to AuthService.handleAuthFailure()
 * which decides to logout only when the device is online — keeping field sessions
 * alive when connectivity drops.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const injector = inject(Injector);

  let token: string | null = null;
  try {
    token = sessionStorage.getItem('access_token');
  } catch {
    // private mode — no header, request will 401 if needed.
  }
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
        const authService = injector.get(AuthService);
        void authService.handleAuthFailure();
      }
      return throwError(() => error);
    })
  );
};
