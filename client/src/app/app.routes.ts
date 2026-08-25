import { Routes } from '@angular/router';
import { authGuard } from './core/auth-guard';
import { platformAdminGuard } from './core/platform-admin-guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login/login').then((m) => m.Login) },
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'tenants', pathMatch: 'full' },
      {
        path: 'tenants',
        canActivate: [platformAdminGuard],
        loadComponent: () => import('./pages/tenants/tenants').then((m) => m.Tenants),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
