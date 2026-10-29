import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService, Role } from './auth.service';

// Lets a signed-in person with the right role through. Anyone else is sent to the sign-in that fits:
// the other role's people go to their own home, signed-out visitors to the login for this area.
const requireRole = (role: Role, login: string, otherHome: string): CanActivateFn => () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (auth.hasRole(role)) {
    return true;
  }
  return router.createUrlTree([auth.isLoggedIn() && auth.role ? otherHome : login]);
};

export const adminGuard = requireRole('Admin', '/admin/login', '/user/home');
export const userGuard = requireRole('User', '/user/login', '/inputForm');
