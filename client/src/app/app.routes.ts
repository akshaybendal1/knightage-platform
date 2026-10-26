import { Routes } from '@angular/router';
import { authGuard } from './core/auth-guard';
import { platformAdminGuard } from './core/platform-admin-guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login/login').then((m) => m.Login) },
  {
    path: 'auth-callback',
    loadComponent: () => import('./pages/auth-callback/auth-callback').then((m) => m.AuthCallback),
  },
  {
    path: 'forgot-password',
    loadComponent: () => import('./pages/forgot-password/forgot-password').then((m) => m.ForgotPassword),
  },
  {
    path: 'reset-password',
    loadComponent: () => import('./pages/reset-password/reset-password').then((m) => m.ResetPassword),
  },
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'services', pathMatch: 'full' },
      {
        path: 'services',
        loadComponent: () => import('./pages/services/services').then((m) => m.Services),
      },
      {
        path: 'tenants',
        canActivate: [platformAdminGuard],
        loadComponent: () => import('./pages/tenants/tenants').then((m) => m.Tenants),
      },
      {
        path: 'plans',
        canActivate: [platformAdminGuard],
        loadComponent: () => import('./pages/plans/plans').then((m) => m.Plans),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
