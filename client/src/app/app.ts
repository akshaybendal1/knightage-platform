import { Component, HostListener, computed, effect, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { Auth } from './core/auth';
import { IdentityApi } from './core/identity-api';
import { Theme } from './core/theme';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css',
})
export class App {
  // See isSignInPath below.
  private readonly onSignInPage = signal(isSignInPath(window.location.pathname));
  protected readonly showShell = computed(() => this.auth.isAuthenticated() && !this.onSignInPage());
  sidebarOpen = signal(false);
  userMenuOpen = signal(false);
  organizationName = signal<string | null>(null);

  constructor(
    protected readonly auth: Auth,
    protected readonly theme: Theme,
    private readonly identityApi: IdentityApi,
    private readonly router: Router,
  ) {
    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe((event) => {
      this.onSignInPage.set(isSignInPath((event as NavigationEnd).urlAfterRedirects));
    });
    // PlatformAdmin is Knightage's own staff, not a tenant -- showing "their" org
    // name would be meaningless, so this only fetches for actual tenant users.
    effect(() => {
      if (this.auth.isAuthenticated() && this.auth.currentUser()?.role !== 'PlatformAdmin') {
        this.loadOrganizationName();
      }
    });
  }

  private loadOrganizationName(): void {
    this.identityApi.getMyOrganization().subscribe({
      next: (org) => this.organizationName.set(org.name),
      error: () => {
        // Non-fatal: the topbar/sidebar just won't show an org name.
      },
    });
  }

  initials(): string {
    const name = this.auth.currentUser()?.displayName ?? '';
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]!.toUpperCase())
      .join('');
  }

  toggleSidebar(): void {
    this.sidebarOpen.update((open) => !open);
  }

  closeSidebar(): void {
    this.sidebarOpen.set(false);
  }

  toggleUserMenu(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    this.userMenuOpen.update((open) => !open);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: Event): void {
    const target = event.target as HTMLElement;
    if (this.userMenuOpen() && !target.closest('.user-menu')) {
      this.userMenuOpen.set(false);
    }
  }
}

// The sign-in pages store the session a moment before they navigate away. Showing the shell as soon as
// the user is authenticated would draw it around the still-visible login form for that moment (an
// "after-image" of the login page), so the shell waits until the router has actually left these pages.
function isSignInPath(url: string): boolean {
  const path = url.split(/[?#]/)[0];
  return path === '/login' || path === '/auth-callback';
}
