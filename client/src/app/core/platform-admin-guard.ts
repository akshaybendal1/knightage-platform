import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { Auth } from './auth';

// Defense in depth for the /tenants route -- the server already rejects non-PlatformAdmins
// with 403 on GET /api/tenants, this just avoids flashing the page before that response
// comes back.
export const platformAdminGuard: CanActivateFn = () => {
  const auth = inject(Auth);
  if (auth.currentUser()?.role === 'PlatformAdmin') {
    return true;
  }
  inject(Router).navigateByUrl('/login');
  return false;
};
