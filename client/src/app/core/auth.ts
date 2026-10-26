import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { AppConfig } from './app-config';
import { AuthResult } from './models';

const STORAGE_KEY = 'knightage_auth';

const CLAIM_NAME = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';
const CLAIM_ROLE = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

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

  forgotPassword(email: string) {
    return this.http.post<{ message: string }>(`${this.appConfig.identityBaseUrl}/api/auth/forgot-password`, {
      email,
      clientResetUrl: `${window.location.origin}/reset-password`,
    });
  }

  resetPassword(email: string, token: string, newPassword: string) {
    return this.http.post<void>(`${this.appConfig.identityBaseUrl}/api/auth/reset-password`, { email, token, newPassword });
  }

  googleSignIn(idToken: string, organizationName?: string) {
    return this.http
      .post<AuthResult>(`${this.appConfig.identityBaseUrl}/api/auth/google`, { idToken, organizationName })
      .pipe(tap((result) => this.setAuth(result)));
  }

  // Handoff from a sibling app (crm, accounting, inventory-sales): it already holds a valid
  // JWT from this same login session and passes it via ?token= on a full-page navigation,
  // since there's no shared session storage between apps. Everything else in AuthResult is
  // read back out of the JWT payload -- no signature verification needed here, the identity
  // service that issued it already signed it and every request re-validates it server-side.
  setTokenFromCallback(token: string): void {
    const claims = this.decodeJwtPayload(token);
    const expiresAtUtc = claims?.['exp']
      ? new Date(claims['exp'] * 1000).toISOString()
      : new Date(Date.now() + 3600_000).toISOString();
    this.setAuth({
      token,
      expiresAtUtc,
      userId: claims?.['sub'] ?? '',
      organizationId: claims?.['org_id'] ?? '',
      email: claims?.['email'] ?? '',
      displayName: claims?.[CLAIM_NAME] ?? claims?.['email'] ?? '',
      role: claims?.[CLAIM_ROLE] ?? '',
    });
  }

  private decodeJwtPayload(token: string): Record<string, any> | null {
    try {
      const payload = token.split('.')[1];
      const base64 = payload.replace(/-/g, '+').replace(/_/g, '/').padEnd(Math.ceil(payload.length / 4) * 4, '=');
      const json = decodeURIComponent(
        atob(base64)
          .split('')
          .map((c) => '%' + c.charCodeAt(0).toString(16).padStart(2, '0'))
          .join(''),
      );
      return JSON.parse(json);
    } catch {
      return null;
    }
  }

  logout(): void {
    this.clearSession();
    this.router.navigateByUrl('/login');
  }

  /**
   * Clears stored auth without navigating. Guards use this: triggering a navigation
   * imperatively from inside a guard while it's still resolving (and then also
   * returning false) races the router against itself -- a guard should instead
   * return a UrlTree redirect (see platform-admin-guard.ts) and let the router own
   * the single resulting navigation.
   */
  clearSession(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.authState.set(null);
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
