import { AfterViewInit, Component, ElementRef, OnInit, signal, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Capacitor } from '@capacitor/core';
import { SocialLogin } from '@capgo/capacitor-social-login';
import { Auth } from '../../core/auth';
import { AppConfig } from '../../core/app-config';
import { loadGoogleIdentityScript } from '../../core/google-identity';

const LOGIN_ERROR_MESSAGES: Record<string, string> = {
  'not-platform-admin':
    "That account doesn't have access to the Knightage Platform admin portal -- it's for platform administrators only.",
};

declare const google: any;

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login implements OnInit, AfterViewInit {
  mode = signal<'login' | 'register'>('login');
  loading = signal(false);
  errorMessage = signal<string | null>(null);
  pendingGoogleIdToken = signal<string | null>(null);
  showPassword = signal(false);

  email = '';
  password = '';
  organizationName = '';
  displayName = '';

  private readonly googleButton = viewChild<ElementRef<HTMLDivElement>>('googleButton');
  private googleReady = false;
  private viewReady = false;

  // Inside the combined Knightage Android app (capacitor.config.ts) this page is the app's sign-in screen.
  // Google blocks its web Identity Services widget in embedded WebViews, so there it renders a plain button
  // that goes through the native Credential Manager flow instead -- same as knightage-crm's login page.
  readonly isNative = Capacitor.isNativePlatform();
  private googleNativeInit: Promise<void> | null = null;

  /** Shown above the form when the last session ended by itself (see Auth.sessionEndedNotice). */
  get sessionNotice(): string | null {
    return this.auth.sessionEndedNotice();
  }

  constructor(
    private readonly auth: Auth,
    private readonly router: Router,
    private readonly route: ActivatedRoute,
    private readonly appConfig: AppConfig,
  ) {}

  get googleClientId(): string | null {
    return this.appConfig.googleClientId;
  }

  ngOnInit(): void {
    // A subscription, not route.snapshot -- the redirect that lands here (from
    // platform-admin-guard) targets this same /login route with a new query param,
    // and the Angular router reuses this already-mounted component instance for
    // that rather than recreating it, so a one-time snapshot read in ngOnInit would
    // miss it. queryParamMap re-emits on every such navigation regardless.
    this.route.queryParamMap.subscribe((params) => {
      const reason = params.get('reason');
      this.errorMessage.set(reason ? (LOGIN_ERROR_MESSAGES[reason] ?? null) : null);
    });

    if (!this.googleClientId) {
      return;
    }

    if (this.isNative) {
      // The Web client ID, not an Android one: Credential Manager issues ID tokens for this audience, which is
      // what identity already validates. The Android OAuth client (package + SHA-1) only has to exist in Google
      // Cloud for Credential Manager to authorize the app; it never appears in code.
      this.googleNativeInit = SocialLogin.initialize({ google: { webClientId: this.googleClientId } });
      this.googleNativeInit.catch(() => {
        // Reported when the button is tapped, not here -- nothing to show until then.
      });
      return;
    }

    loadGoogleIdentityScript()
      .then(() => {
        google.accounts.id.initialize({
          client_id: this.googleClientId,
          callback: (response: { credential: string }) => this.onGoogleCredential(response.credential),
        });
        this.googleReady = true;
        this.renderGoogleButtonIfReady();
      })
      .catch(() => {
        // No Google button rather than a broken page -- everything else about
        // login/register works fine without it.
      });
  }

  ngAfterViewInit(): void {
    this.viewReady = true;
    this.renderGoogleButtonIfReady();
  }

  private renderGoogleButtonIfReady(): void {
    if (!this.googleReady || !this.viewReady) {
      return;
    }
    const el = this.googleButton()?.nativeElement;
    if (el) {
      google.accounts.id.renderButton(el, { theme: 'outline', size: 'large', width: 320 });
    }
  }

  toggleMode(): void {
    this.mode.set(this.mode() === 'login' ? 'register' : 'login');
    this.errorMessage.set(null);
  }

  submit(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    const request =
      this.mode() === 'login'
        ? this.auth.login(this.email, this.password)
        : this.auth.register(this.organizationName, this.email, this.password, this.displayName);

    request.subscribe({
      next: () => {
        // Stays 'loading' until the next page replaces this one, so the button doesn't re-enable in between.
        // replaceUrl: once signed in, Back must not return to this form (in the Android app it leaves the app instead).
        this.router.navigateByUrl('/', { replaceUrl: true });
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Something went wrong. Check your details and try again.');
      },
    });
  }

  async signInWithGoogleNative(): Promise<void> {
    this.errorMessage.set(null);
    try {
      await this.googleNativeInit;
      // No `scopes`: the plugin already requests email/profile/openid, and any scopes array makes it reject unless MainActivity is modified.
      const { result } = await SocialLogin.login({ provider: 'google', options: {} });
      const idToken = (result as { idToken?: string | null } | undefined)?.idToken;
      if (!idToken) {
        this.errorMessage.set('Could not sign in with Google.');
        return;
      }
      this.onGoogleCredential(idToken);
    } catch (err) {
      if ((err as { code?: string } | null)?.code === 'USER_CANCELLED') {
        return;
      }
      console.error('Native Google sign-in failed', err);
      this.errorMessage.set("Google sign-in isn't available right now. Sign in with your email and password, or try again later.");
    }
  }

  private onGoogleCredential(idToken: string): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.auth.googleSignIn(idToken).subscribe({
      next: () => {
        // Stays 'loading' until the next page replaces this one, so the button doesn't re-enable in between.
        // replaceUrl: once signed in, Back must not return to this form (in the Android app it leaves the app instead).
        this.router.navigateByUrl('/', { replaceUrl: true });
      },
      error: (err) => {
        this.loading.set(false);
        if (err?.error?.requiresOrganizationName) {
          this.organizationName = '';
          this.pendingGoogleIdToken.set(idToken);
        } else {
          this.errorMessage.set(err?.error?.message ?? 'Could not sign in with Google.');
        }
      },
    });
  }

  continueGoogleSignUp(): void {
    const idToken = this.pendingGoogleIdToken();
    if (!idToken) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.auth.googleSignIn(idToken, this.organizationName).subscribe({
      next: () => {
        // Stays 'loading' until the next page replaces this one, so the button doesn't re-enable in between.
        // replaceUrl: once signed in, Back must not return to this form (in the Android app it leaves the app instead).
        this.router.navigateByUrl('/', { replaceUrl: true });
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Could not finish creating your account.');
      },
    });
  }

  cancelGoogleSignUp(): void {
    this.pendingGoogleIdToken.set(null);
    this.errorMessage.set(null);
  }
}
