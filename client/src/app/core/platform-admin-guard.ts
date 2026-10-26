import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { Auth } from './auth';

// Defense in depth for the /tenants route -- the server already rejects non-PlatformAdmins
// with 403 on GET /api/tenants, this just avoids flashing the page before that response
// comes back. A valid tenant login that isn't a PlatformAdmin has nowhere else to go in this
// app, so log them out (rather than leaving them authenticated but stuck) and surface a clear
// reason on the login page instead of silently bouncing back to a blank form. Returning a
// UrlTree (rather than imperatively navigating + returning false) makes the redirect part of
// this same navigation instead of racing a second one against it.
export const platformAdminGuard: CanActivateFn = () => {
  const auth = inject(Auth);
  if (auth.currentUser()?.role === 'PlatformAdmin') {
    return true;
  }
  auth.clearSession();
  return inject(Router).createUrlTree(['/login'], { queryParams: { reason: 'not-platform-admin' } });
};
