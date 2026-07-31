import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { Auth } from './auth';

export const authGuard: CanActivateFn = () => {
  const auth = inject(Auth);
  if (auth.isAuthenticated()) {
    return true;
  }
  inject(Router).navigateByUrl('/login');
  return false;
};
