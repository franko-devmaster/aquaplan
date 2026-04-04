import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

export const featureGuard = (requiredRoles: string[]): CanActivateFn => {
  return async () => {
    const authService = inject(AuthService);
    const router = inject(Router);

    await authService.whenUserLoaded();

    const user = authService.currentUser();
    if (!user) {
      return router.createUrlTree(['/login']);
    }

    const hasRole = requiredRoles.some((role) => user.roles.includes(role));
    if (hasRole) {
      return true;
    }

    return router.createUrlTree(['/']);
  };
};
