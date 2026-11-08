import { Injectable, computed, signal } from '@angular/core';
import { HttpBackend, HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { firstValueFrom, tap } from 'rxjs';
import { AppConfig } from './app-config';
import { AuthResult } from './models';

// Same session handling in knightage-crm, knightage-platform, knightage-inventory-sales and knightage-accounting
// (core/auth.ts + core/auth-interceptor.ts) -- keep the copies in step.
//
// A sign-in is a short-lived access token (60 minutes on production) plus a refresh token. Before the access token
// runs out, the next request first trades the refresh token for a new pair (Identity's POST /api/auth/refresh), so
// people stay signed in while they use the app. When Identity says the session is over (unused for 30 days, 90 days
// old, signed out elsewhere, password reset, deactivated), the app goes to its sign-in page and says so.

const STORAGE_KEY = 'knightage_auth';
/** Held while a tab renews, so two tabs of this app never trade the same refresh token at once. */
const RENEW_LOCK = 'knightage_auth_renew';
/** Renew this long before the access token runs out, so no request leaves with a token about to expire. */
const RENEW_EARLY_MS = 2 * 60_000;

export const SESSION_ENDED_MESSAGE = 'Your session has ended. Sign in again.';

const CLAIM_NAME = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';
const CLAIM_ROLE = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

/** What this app keeps about the signed-in person. The two times are in this device's clock (see stamp()). */
type StoredAuth = AuthResult & {
  /** When to renew the access token. */
  renewAt?: number;
  /** When the refresh token stops working if unused. */
  refreshUntil?: number;
};

@Injectable({
  providedIn: 'root',
})
export class Auth {
  /** Set when a session ends by itself; the sign-in page shows it, and signing in again clears it. */
  readonly sessionEndedNotice = signal<string | null>(null);

  private readonly authState = signal<StoredAuth | null>(this.loadStoredAuth());

  readonly currentUser = computed(() => this.authState());
  readonly isAuthenticated = computed(() => this.authState() !== null);

  /** For Identity's session calls: bypasses the interceptors, which would otherwise try to renew for them. */
  private readonly sessionHttp: HttpClient;
  private renewal: Promise<string | null> | null = null;

  constructor(
    private readonly http: HttpClient,
    httpBackend: HttpBackend,
    private readonly router: Router,
    private readonly appConfig: AppConfig,
  ) {
    this.sessionHttp = new HttpClient(httpBackend);
    // Another tab of this app signed in, renewed or signed out: follow it.
    window.addEventListener('storage', (event) => {
      if (event.key === STORAGE_KEY) {
        this.followOtherTab(event.newValue);
      }
    });
  }

  get token(): string | null {
    return this.authState()?.token ?? null;
  }

  login(email: string, password: string) {
    return this.http
      .post<AuthResult>(`${this.appConfig.identityBaseUrl}/api/auth/login`, { email, password })
      .pipe(tap((result) => this.startSession(result)));
  }

  register(organizationName: string, email: string, password: string, displayName: string) {
    return this.http
      .post<AuthResult>(`${this.appConfig.identityBaseUrl}/api/auth/register`, {
        organizationName,
        email,
        password,
        displayName,
      })
      .pipe(tap((result) => this.startSession(result)));
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
      .pipe(tap((result) => this.startSession(result)));
  }

  /** Arriving from another Knightage app: redeems its one-time code (see handoffUrl()) for a session of this app's own. */
  redeemHandoff(code: string) {
    return this.sessionHttp
      .post<AuthResult>(`${this.appConfig.identityBaseUrl}/api/auth/handoff/redeem`, { code })
      .pipe(tap((result) => this.startSession(result)));
  }

  // The older hand-over: another Knightage app passes its access token itself via ?token=. Still accepted from apps
  // that don't send a code yet. Everything else in AuthResult is read back out of the JWT payload -- no signature
  // check needed here, every API re-validates the token. There is no refresh token, so this session ends when the
  // token expires.
  setTokenFromCallback(token: string): void {
    const claims = this.decodeJwtPayload(token);
    const expiresAtUtc = claims?.['exp']
      ? new Date(claims['exp'] * 1000).toISOString()
      : new Date(Date.now() + 3600_000).toISOString();
    this.startSession({
      token,
      expiresAtUtc,
      userId: claims?.['sub'] ?? '',
      organizationId: claims?.['org_id'] ?? '',
      email: claims?.['email'] ?? '',
      displayName: claims?.[CLAIM_NAME] ?? claims?.['email'] ?? '',
      role: claims?.[CLAIM_ROLE] ?? '',
    });
  }

  /**
   * The access token to send now, renewed first when it is about to run out. Null when there is no session any more
   * (the sign-in page is then on its way). Rejects when Identity can't be reached: the session is kept, and the request
   * that needed the token fails like any other request made offline.
   */
  async validToken(): Promise<string | null> {
    const current = this.authState();
    if (!current) {
      return null;
    }
    if (Date.now() < this.renewAt(current)) {
      return current.token;
    }
    if (!current.refreshToken) {
      // Signed in through the older hand-over: use the token until it really expires, then the session is over.
      if (Date.now() < this.renewAt(current) + RENEW_EARLY_MS) {
        return current.token;
      }
      this.endSession();
      return null;
    }
    return this.renew();
  }

  /** A request came back 401 with `rejected`: renew, unless another request already has. Null: the session is over. */
  async tokenAfterRejection(rejected: string): Promise<string | null> {
    const current = this.authState();
    if (!current) {
      return null;
    }
    if (current.token !== rejected) {
      return current.token;
    }
    if (!current.refreshToken) {
      this.endSession();
      return null;
    }
    return this.renew(rejected);
  }

  /**
   * Address that opens another Knightage app already signed in: its /auth-callback with a one-time code from Identity,
   * which that app redeems for a session of its own. Without a session it is just the app's address (its sign-in page);
   * if Identity can't be reached, the older ?token= hand-over.
   */
  async handoffUrl(appBaseUrl: string): Promise<string> {
    let token: string | null;
    try {
      token = await this.validToken();
    } catch {
      token = this.token;
    }
    if (!token) {
      return appBaseUrl;
    }
    // The token still rides along so an app that doesn't redeem codes yet (mid-deployment) signs in as before; apps
    // that do ignore it. Drop it once every app reads ?code=.
    const legacy = `token=${encodeURIComponent(token)}`;
    try {
      const { code } = await firstValueFrom(
        this.sessionHttp.post<{ code: string }>(`${this.appConfig.identityBaseUrl}/api/auth/handoff`, {}, {
          headers: { Authorization: `Bearer ${token}` },
        }),
      );
      return `${appBaseUrl}/auth-callback?code=${encodeURIComponent(code)}&${legacy}`;
    } catch {
      return `${appBaseUrl}/auth-callback?${legacy}`;
    }
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
    // Replace, don't push: the signed-out page takes the place of the page signed out from. Pushed, it left an extra
    // entry behind, so in the Android app Back on the next user's home went to another copy of it instead of leaving.
    this.router.navigateByUrl('/login', { replaceUrl: true });
  }

  /**
   * Signs out without navigating. Guards use this: triggering a navigation
   * imperatively from inside a guard while it's still resolving (and then also
   * returning false) races the router against itself -- a guard should instead
   * return a UrlTree redirect (see platform-admin-guard.ts) and let the router own
   * the single resulting navigation.
   */
  clearSession(): void {
    this.closeSessionAtIdentity(this.authState()?.refreshToken);
    this.forget();
    this.sessionEndedNotice.set(null);
  }

  /**
   * One renewal at a time in this tab, and (where the browser supports locks) across this app's tabs.
   * `rejected`: a token the server refused, so not to be reused even if it looks current.
   */
  private renew(rejected?: string): Promise<string | null> {
    this.renewal ??= this.withRenewLock(() => this.renewNow(rejected)).finally(() => (this.renewal = null));
    return this.renewal;
  }

  private async renewNow(rejected?: string): Promise<string | null> {
    // Another tab may have renewed (or signed out) while this one waited for the lock: storage has its result.
    const stored = this.readStorage();
    if (!stored) {
      this.endSession();
      return null;
    }
    if (stored.token !== rejected && Date.now() < this.renewAt(stored)) {
      if (stored.token !== this.authState()?.token) {
        this.authState.set(stored);
      }
      return stored.token;
    }
    if (!stored.refreshToken) {
      this.endSession();
      return null;
    }

    try {
      const result = await firstValueFrom(
        this.sessionHttp.post<AuthResult>(`${this.appConfig.identityBaseUrl}/api/auth/refresh`, {
          refreshToken: stored.refreshToken,
        }),
      );
      this.persist(this.stamp(result));
      return result.token;
    } catch (error) {
      if (error instanceof HttpErrorResponse && (error.status === 401 || error.status === 400)) {
        this.endSession();
        return null;
      }
      throw error;
    }
  }

  private withRenewLock<T>(work: () => Promise<T>): Promise<T> {
    // request() resolves with what the callback's promise resolves with; its typing doesn't unwrap that promise.
    return 'locks' in navigator ? (navigator.locks.request(RENEW_LOCK, work) as unknown as Promise<T>) : work();
  }

  /** A new sign-in in this app. Closes the session it replaces, if any, so that one's refresh token stops working. */
  private startSession(result: AuthResult): void {
    const previous = this.authState()?.refreshToken;
    this.persist(this.stamp(result));
    this.sessionEndedNotice.set(null);
    if (previous && previous !== result.refreshToken) {
      this.closeSessionAtIdentity(previous);
    }
  }

  /** The session ended by itself (or in another app/tab): back to the sign-in page, saying why. */
  private endSession(): void {
    if (!this.authState() && !localStorage.getItem(STORAGE_KEY)) {
      return;
    }
    this.forget();
    this.sessionEndedNotice.set(SESSION_ENDED_MESSAGE);
    this.router.navigateByUrl('/login', { replaceUrl: true });
  }

  private closeSessionAtIdentity(refreshToken: string | null | undefined): void {
    if (!refreshToken) {
      return;
    }
    this.sessionHttp
      .post(`${this.appConfig.identityBaseUrl}/api/auth/logout`, { refreshToken })
      .subscribe({ error: () => undefined });
  }

  private followOtherTab(raw: string | null): void {
    const next = this.parse(raw);
    const current = this.authState();
    if (!next) {
      if (current) {
        this.authState.set(null);
        this.router.navigateByUrl('/login', { replaceUrl: true });
      }
      return;
    }
    if (current && current.userId !== next.userId) {
      // Someone else signed in on another tab: start this one over as them rather than mix two people's data.
      location.assign('/');
      return;
    }
    this.authState.set(next);
  }

  /**
   * Times the session in this device's clock. Identity says how many seconds the token has left, so a phone whose clock
   * is off still renews on time (comparing expiresAtUtc with Date.now() would renew constantly, or never).
   */
  private stamp(result: AuthResult): StoredAuth {
    const now = Date.now();
    const serverNow = result.expiresInSeconds != null ? Date.parse(result.expiresAtUtc) - result.expiresInSeconds * 1000 : now;
    const offset = now - serverNow;
    return {
      ...result,
      renewAt: Date.parse(result.expiresAtUtc) + offset - RENEW_EARLY_MS,
      refreshUntil: result.refreshTokenExpiresAtUtc ? Date.parse(result.refreshTokenExpiresAtUtc) + offset : undefined,
    };
  }

  private renewAt(auth: StoredAuth): number {
    return auth.renewAt ?? Date.parse(auth.expiresAtUtc) - RENEW_EARLY_MS;
  }

  private persist(auth: StoredAuth): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(auth));
    this.authState.set(auth);
  }

  private forget(): void {
    localStorage.removeItem(STORAGE_KEY);
    this.authState.set(null);
  }

  private readStorage(): StoredAuth | null {
    return this.parse(localStorage.getItem(STORAGE_KEY));
  }

  private parse(raw: string | null): StoredAuth | null {
    if (!raw) {
      return null;
    }
    try {
      const parsed = JSON.parse(raw) as StoredAuth;
      return typeof parsed?.token === 'string' ? parsed : null;
    } catch {
      return null;
    }
  }

  private loadStoredAuth(): StoredAuth | null {
    const stored = this.readStorage();
    if (!stored) {
      localStorage.removeItem(STORAGE_KEY);
      return null;
    }
    const now = Date.now();
    // An access token that ran out while the app was closed is fine if the session can still be renewed: the first
    // request renews it.
    const renewable = !!stored.refreshToken && (stored.refreshUntil === undefined || now < stored.refreshUntil);
    if (renewable || now < this.renewAt(stored) + RENEW_EARLY_MS) {
      return stored;
    }
    localStorage.removeItem(STORAGE_KEY);
    this.sessionEndedNotice.set(SESSION_ENDED_MESSAGE);
    return null;
  }
}
