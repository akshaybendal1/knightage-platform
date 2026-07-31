import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { AppConfig } from './app-config';
import { AuthResult } from './models';

const STORAGE_KEY = 'knightage_auth';

@Injectable({
  providedIn: 'root',
})
export class Auth {
  private readonly authState = signal<AuthResult | null>(this.loadStoredAuth());

  readonly currentUser = computed(() => this.authState());
  readonly isAuthenticated = computed(() => this.authState() !== null);

  constructor(
    private readonly http: HttpClient,
    private readonly router: Router,
    private readonly appConfig: AppConfig,
  ) {}

  get token(): string | null {
    return this.authState()?.token ?? null;
  }

  login(email: string, password: string) {
    return this.http
      .post<AuthResult>(`${this.appConfig.identityBaseUrl}/api/auth/login`, { email, password })
      .pipe(tap((result) => this.setAuth(result)));
  }

  register(organizationName: string, email: string, password: string, displayName: string) {
    return this.http
      .post<AuthResult>(`${this.appConfig.identityBaseUrl}/api/auth/register`, {
        organizationName,
        email,
        password,
        displayName,
      })
      .pipe(tap((result) => this.setAuth(result)));
  }

  logout(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.authState.set(null);
    this.router.navigateByUrl('/login');
  }

  private setAuth(result: AuthResult): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(result));
    this.authState.set(result);
  }

  private loadStoredAuth(): AuthResult | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }

    try {
      const parsed = JSON.parse(raw) as AuthResult;
      if (new Date(parsed.expiresAtUtc).getTime() < Date.now()) {
        localStorage.removeItem(STORAGE_KEY);
        return null;
      }
      return parsed;
    } catch {
      localStorage.removeItem(STORAGE_KEY);
      return null;
    }
  }
}
