import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Auth } from '../../core/auth';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class Login {
  mode = signal<'login' | 'register'>('login');
  loading = signal(false);
  errorMessage = signal<string | null>(null);

  email = '';
  password = '';
  organizationName = '';
  displayName = '';

  constructor(
    private readonly auth: Auth,
    private readonly router: Router,
  ) {}

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
        this.loading.set(false);
        this.router.navigateByUrl('/tenants');
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Something went wrong. Check your details and try again.');
      },
    });
  }
}
