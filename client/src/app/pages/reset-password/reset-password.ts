import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Auth } from '../../core/auth';

@Component({
  selector: 'app-reset-password',
  imports: [FormsModule, RouterLink],
  templateUrl: './reset-password.html',
  styleUrl: './reset-password.css',
})
export class ResetPassword implements OnInit {
  email = '';
  token = '';
  newPassword = '';
  confirmPassword = '';
  loading = signal(false);
  errorMessage = signal<string | null>(null);
  linkInvalid = signal(false);
  showNewPassword = signal(false);
  showConfirmPassword = signal(false);

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    private readonly auth: Auth,
  ) {}

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    this.email = params.get('email') ?? '';
    this.token = params.get('token') ?? '';
    if (!this.email || !this.token) {
      this.linkInvalid.set(true);
    }
  }

  submit(): void {
    this.errorMessage.set(null);
    if (this.newPassword !== this.confirmPassword) {
      this.errorMessage.set('Passwords do not match.');
      return;
    }

    this.loading.set(true);
    this.auth.resetPassword(this.email, this.token, this.newPassword).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigateByUrl('/login');
      },
      error: (err) => {
        this.loading.set(false);
        if (err?.status === 400) {
          this.linkInvalid.set(true);
        } else {
          this.errorMessage.set(err?.error?.message ?? 'Something went wrong. Try again.');
        }
      },
    });
  }
}
