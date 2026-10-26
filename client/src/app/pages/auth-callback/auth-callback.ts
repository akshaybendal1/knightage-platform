import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Auth } from '../../core/auth';

// Public route (outside authGuard) that a sibling app (crm, accounting, inventory-sales)
// redirects to with ?token=<jwt> when the user clicks "Open Knightage Platform" from their
// own organization page. Decodes and stores the token the same way a direct login does,
// then lands the user straight on their services list.
@Component({
  selector: 'app-auth-callback',
  imports: [],
  templateUrl: './auth-callback.html',
})
export class AuthCallback implements OnInit {
  errorMessage: string | null = null;

  constructor(
    private readonly route: ActivatedRoute,
    private readonly auth: Auth,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    const token = this.route.snapshot.queryParamMap.get('token');
    if (!token) {
      this.errorMessage = 'No token was provided in the redirect.';
      return;
    }

    try {
      this.auth.setTokenFromCallback(token);
      this.router.navigateByUrl('/services');
    } catch {
      this.errorMessage = 'Could not complete sign-in with the provided token.';
    }
  }
}
