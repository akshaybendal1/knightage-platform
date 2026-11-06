import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { Auth } from './auth';

/** The Users screen is for an organization's Owner and Admins; anyone else goes back to their services. */
export const orgAdminGuard: CanActivateFn = () => {
  const role = inject(Auth).currentUser()?.role;
  return role === 'Owner' || role === 'Admin' ? true : inject(Router).parseUrl('/services');
};
