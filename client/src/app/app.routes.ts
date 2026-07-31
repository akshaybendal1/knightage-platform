import { Routes } from '@angular/router';
import { authGuard } from './core/auth-guard';

export const routes: Routes = [
  { path: 'login', loadComponent: () => import('./pages/login/login').then((m) => m.Login) },
  {
    path: '',
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'tenants', pathMatch: 'full' },
      { path: 'tenants', loadComponent: () => import('./pages/tenants/tenants').then((m) => m.Tenants) },
    ],
  },
  { path: '**', redirectTo: '' },
];
