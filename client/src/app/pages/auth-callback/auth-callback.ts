import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Auth } from '../../core/auth';

// Public route (outside authGuard) that a sibling app (crm, accounting, inventory-sales) redirects to when the
// user opens Platform from it: ?code=<one-time code> is redeemed with Identity for Platform's own session;
// ?token=<jwt> is the older hand-over, still accepted. Then lands the user on their services list.
@Component({
  selector: 'app-auth-callback',
  imports: [],
  templateUrl: './auth-callback.html',
})
export class AuthCallback implements OnInit {
  // A signal: the message can now arrive after the code is checked, and these apps have no zone.js to notice a
  // plain field changing then.
  readonly errorMessage = signal<string | null>(null);

  constructor(
    private readonly route: ActivatedRoute,
    private readonly auth: Auth,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const code = params.get('code');
    const token = params.get('token');

    if (code) {
      // A one-time code from the app the user came from: redeemed for a session of this app's own.
      this.auth.redeemHandoff(code).subscribe({
        next: () => this.enter(),
        error: () => {
          // Expired or already used (e.g. this page reopened from history). A token sent alongside still works.
          if (!token || !this.signInWithToken(token)) {
            this.errorMessage.set('This sign-in link has expired. Sign in again.');
          }
        },
      });
      return;
    }

    if (!token) {
      this.errorMessage.set('No token was provided in the redirect.');
      return;
    }
    if (!this.signInWithToken(token)) {
      this.errorMessage.set('Could not complete sign-in with the provided token.');
    }
  }

  private signInWithToken(token: string): boolean {
    try {
      this.auth.setTokenFromCallback(token);
      this.enter();
      return true;
    } catch {
      return false;
    }
  }

  private enter(): void {
    // Replace, don't push: otherwise Back lands on /auth-callback again, which signs in and moves forward
    // again -- a loop that traps Back inside this app (and keeps the code in the history).
    this.router.navigateByUrl('/services', { replaceUrl: true });
  }
}
