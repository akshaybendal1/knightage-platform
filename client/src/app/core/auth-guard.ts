import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { Auth } from './auth';

// Returned as a redirect rather than started with navigateByUrl(): a returned redirect inherits the original
// navigation's replaceUrl (the first load always replaces), so "/" doesn't stay in history under /login --
// the Android app's Back button would otherwise land on it.
export const authGuard: CanActivateFn = () => {
  const auth = inject(Auth);
  if (auth.isAuthenticated()) {
    return true;
  }
  return inject(Router).parseUrl('/login');
};
