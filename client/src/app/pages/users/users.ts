import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { Auth } from '../../core/auth';
import { IdentityApi } from '../../core/identity-api';
import { PlatformApi } from '../../core/platform-api';
import { AccessAppDef, TeamMember, UserAccessSummary } from '../../core/models';

/**
 * The organization's people and, at a glance, which apps each may open. Opening a person leads to their
 * access (users/:id). The people themselves live in knightage-identity; their app access in this app.
 */
@Component({
  selector: 'app-users',
  imports: [FormsModule, RouterLink],
  templateUrl: './users.html',
  styleUrl: './users.css',
})
export class Users implements OnInit {
  members = signal<TeamMember[]>([]);
  apps = signal<AccessAppDef[]>([]);
  summaries = signal<Map<string, UserAccessSummary>>(new Map());
  loading = signal(true);
  errorMessage = signal<string | null>(null);
  search = signal('');

  showForm = signal(false);
  adding = signal(false);
  addError = signal<string | null>(null);
  added = signal<string | null>(null);
  displayName = '';
  email = '';
  phone = '';
  password = '';
  role = 'Member';

  readonly enabledApps = computed(() => this.apps().filter((a) => a.enabled));

  readonly filtered = computed(() => {
    const q = this.search().trim().toLowerCase();
    const list = [...this.members()].sort((a, b) =>
      a.role === 'Owner' ? -1 : b.role === 'Owner' ? 1 : Number(b.isActive) - Number(a.isActive) || a.displayName.localeCompare(b.displayName),
    );
    return q ? list.filter((m) => `${m.displayName} ${m.email} ${m.role}`.toLowerCase().includes(q)) : list;
  });

  constructor(
    private readonly identityApi: IdentityApi,
    private readonly api: PlatformApi,
    protected readonly auth: Auth,
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    forkJoin({
      team: this.identityApi.getTeam(),
      catalog: this.api.getAccessCatalog(),
      summaries: this.api.getAccessSummaries(),
    }).subscribe({
      next: ({ team, catalog, summaries }) => {
        this.members.set(team);
        this.apps.set(catalog.apps);
        this.summaries.set(new Map(summaries.items.map((s) => [s.userId, s])));
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Could not load your users. Check your connection and try again.');
        this.loading.set(false);
      },
    });
  }

  /** May this person open the app? The Owner always may; anyone else unless an admin took it away. */
  canOpen(member: TeamMember, service: string): boolean {
    return member.role === 'Owner' || this.summaries().get(member.id)?.apps[service] !== false;
  }

  areaExceptions(member: TeamMember): number {
    return member.role === 'Owner' ? 0 : (this.summaries().get(member.id)?.areaExceptions ?? 0);
  }

  isSelf(member: TeamMember): boolean {
    return member.id === this.auth.currentUser()?.userId;
  }

  initials(name: string): string {
    return name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]!.toUpperCase())
      .join('');
  }

  generatePassword(): void {
    const chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789';
    const bytes = new Uint32Array(12);
    crypto.getRandomValues(bytes);
    this.password = Array.from(bytes, (b) => chars[b % chars.length]).join('') + '#7';
  }

  addMember(): void {
    this.addError.set(null);
    this.added.set(null);
    if (!this.displayName.trim() || !this.email.trim() || this.password.length < 8) {
      this.addError.set('Enter a name, an email and a password of at least 8 characters.');
      return;
    }
    this.adding.set(true);
    this.identityApi
      .addTeamMember({
        displayName: this.displayName.trim(),
        email: this.email.trim(),
        password: this.password,
        role: this.role,
        phoneNumber: this.phone.trim() || undefined,
      })
      .subscribe({
        next: (member) => {
          this.adding.set(false);
          this.members.update((list) => [...list, member]);
          this.added.set(`${member.displayName} was added as ${member.role}. Share the password with them; they can change it after signing in.`);
          this.displayName = this.email = this.phone = this.password = '';
          this.role = 'Member';
          this.showForm.set(false);
        },
        error: (err) => {
          this.adding.set(false);
          this.addError.set(err?.error?.message ?? 'Could not add this user.');
        },
      });
  }
}
