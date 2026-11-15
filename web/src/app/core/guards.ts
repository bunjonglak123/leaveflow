import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth';
import { Role } from './models';

/** Requires a signed-in user; otherwise go to /login. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.isLoggedIn() ? true : inject(Router).parseUrl('/login');
};

/** Requires one of <roles>; signed-in users without it go to their own home page. */
export function roleGuard(...roles: Role[]): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    if (!auth.isLoggedIn()) return router.parseUrl('/login');
    return auth.hasRole(...roles) ? true : router.parseUrl(auth.homeUrl());
  };
}
