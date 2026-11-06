import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { Auth } from '../../core/auth';
import { IdentityApi } from '../../core/identity-api';
import { PlatformApi } from '../../core/platform-api';
import { actionKey, appKey, defaultsFor } from '../../core/access-model';
import { AccessAppDef, AccessFeatureDef, TeamMember } from '../../core/models';

interface FeatureGroup {
  name: string;
  features: AccessFeatureDef[];
}

/**
 * One person's access: their role and status (knightage-identity), which apps they may open, and what they
 * may do in each area of each app. Every box starts at what their role allows; only boxes changed from that
 * are stored, so "Reset to role defaults" simply removes their exceptions.
 */
@Component({
  selector: 'app-user-access',
  imports: [FormsModule, RouterLink],
  templateUrl: './user-access.html',
  styleUrl: './user-access.css',
})
export class UserAccess implements OnInit {
  readonly userId: string;

  member = signal<TeamMember | null>(null);
  apps = signal<AccessAppDef[]>([]);
  /** Exceptions as stored. */
  stored = signal<Record<string, boolean>>({});
  /** Every key with the value on screen. */
  draft = signal<Map<string, boolean>>(new Map());

  loading = signal(true);
  notFound = signal(false);
  errorMessage = signal<string | null>(null);
  saving = signal(false);
  savedMessage = signal<string | null>(null);

  /** Folded app sections. On a phone they start folded: all three open make a very long page. */
  collapsed = signal<Set<string>>(new Set(window.innerWidth < 576 ? ['Crm', 'InventorySales', 'Accounting'] : []));

  roleDraft = '';
  savingRole = signal(false);
  savingStatus = signal(false);
  personMessage = signal<string | null>(null);

  readonly role = computed(() => this.member()?.role ?? '');
  readonly enabledApps = computed(() => this.apps().filter((a) => a.enabled));
  readonly disabledApps = computed(() => this.apps().filter((a) => !a.enabled));
  readonly defaults = computed(() => defaultsFor(this.apps(), this.role()));
  readonly isOwner = computed(() => this.role() === 'Owner');
  readonly isSelf = computed(() => this.member()?.id === this.auth.currentUser()?.userId);
  readonly editable = computed(() => !!this.member() && !this.isOwner() && !this.isSelf());

  /** Keys whose value differs from the role's default -- exactly what Save stores. */
  readonly exceptions = computed(() => this.exceptionsOf(this.draft()));

  readonly dirty = computed(() => !sameMap(this.exceptions(), this.exceptionsOf(this.buildDraft(this.stored()))));
  readonly exceptionCount = computed(() => Object.keys(this.exceptions()).length);

  constructor(
    route: ActivatedRoute,
    private readonly identityApi: IdentityApi,
    private readonly api: PlatformApi,
    private readonly auth: Auth,
  ) {
    this.userId = route.snapshot.paramMap.get('id') ?? '';
  }

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    forkJoin({
      team: this.identityApi.getTeam(),
      catalog: this.api.getAccessCatalog(),
      access: this.api.getUserAccess(this.userId),
    }).subscribe({
      next: ({ team, catalog, access }) => {
        const member = team.find((m) => m.id === this.userId) ?? null;
        this.member.set(member);
        this.notFound.set(member === null);
        this.roleDraft = member?.role ?? '';
        this.apps.set(catalog.apps);
        this.stored.set(access.overrides);
        this.resetDraft();
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Could not load this user. Check your connection and try again.');
        this.loading.set(false);
      },
    });
  }

  groups(app: AccessAppDef): FeatureGroup[] {
    const groups: FeatureGroup[] = [];
    for (const feature of app.features) {
      let group = groups.find((g) => g.name === feature.group);
      if (!group) {
        group = { name: feature.group, features: [] };
        groups.push(group);
      }
      group.features.push(feature);
    }
    return groups;
  }

  isCollapsed(app: AccessAppDef): boolean {
    return this.collapsed().has(app.service);
  }

  toggleCollapsed(app: AccessAppDef): void {
    this.collapsed.update((set) => {
      const next = new Set(set);
      if (!next.delete(app.service)) {
        next.add(app.service);
      }
      return next;
    });
  }

  // ---- reading the draft ----

  canOpen(app: AccessAppDef): boolean {
    return this.draft().get(appKey(app.service)) ?? true;
  }

  value(app: AccessAppDef, feature: AccessFeatureDef, action: string): boolean {
    return this.draft().get(actionKey(app.service, feature.key, action)) ?? false;
  }

  /** Changed from the role's default (highlighted, and what Save stores). */
  changed(key: string): boolean {
    return this.exceptions()[key] !== undefined;
  }

  actionChanged(app: AccessAppDef, feature: AccessFeatureDef, action: string): boolean {
    return this.changed(actionKey(app.service, feature.key, action));
  }

  roleDefaultText(app: AccessAppDef, feature: AccessFeatureDef): string {
    return this.defaults().get(actionKey(app.service, feature.key, 'view')) ? 'Role default: everything' : 'Role default: no access';
  }

  appExceptionCount(app: AccessAppDef): number {
    return Object.keys(this.exceptions()).filter((k) => k === app.service || k.startsWith(`${app.service}:`)).length;
  }

  // ---- changing the draft ----

  setApp(app: AccessAppDef, allowed: boolean): void {
    this.update((map) => map.set(appKey(app.service), allowed));
  }

  /**
   * Ticking Add/Edit/Delete also ticks View (you can't change what you can't see); unticking View clears the
   * rest -- the apps enforce the same rule.
   */
  toggle(app: AccessAppDef, feature: AccessFeatureDef, action: string, allowed: boolean): void {
    this.update((map) => {
      map.set(actionKey(app.service, feature.key, action), allowed);
      if (action === 'view' && !allowed) {
        for (const a of feature.actions) {
          map.set(actionKey(app.service, feature.key, a.key), false);
        }
      }
      if (action !== 'view' && allowed) {
        map.set(actionKey(app.service, feature.key, 'view'), true);
      }
    });
  }

  /** Whole app at once: 'all' = everything, 'view' = look but not change, 'none' = nothing, 'defaults' = the role's. */
  setAll(app: AccessAppDef, mode: 'all' | 'view' | 'none' | 'defaults'): void {
    const defaults = this.defaults();
    this.update((map) => {
      for (const feature of app.features) {
        for (const action of feature.actions) {
          const key = actionKey(app.service, feature.key, action.key);
          const value = mode === 'defaults' ? (defaults.get(key) ?? false) : mode === 'all' || (mode === 'view' && action.key === 'view');
          map.set(key, value);
        }
      }
      if (mode === 'defaults') {
        map.set(appKey(app.service), true);
      }
    });
  }

  discard(): void {
    this.resetDraft();
    this.savedMessage.set(null);
  }

  save(): void {
    const member = this.member();
    if (!member || !this.editable()) {
      return;
    }
    this.saving.set(true);
    this.errorMessage.set(null);
    this.savedMessage.set(null);
    const overrides = this.exceptions();
    this.api.saveUserAccess(member.id, overrides).subscribe({
      next: () => {
        this.saving.set(false);
        this.stored.set(overrides);
        this.savedMessage.set(`Saved. ${member.displayName} gets the new access within a minute (or when they next open the app).`);
      },
      error: (err) => {
        this.saving.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Could not save. Nothing was changed.');
      },
    });
  }

  // ---- the person (knightage-identity) ----

  saveRole(): void {
    const member = this.member();
    if (!member || this.roleDraft === member.role) {
      return;
    }
    this.savingRole.set(true);
    this.personMessage.set(null);
    this.identityApi.updateRole(member.id, this.roleDraft).subscribe({
      next: () => {
        this.savingRole.set(false);
        this.member.set({ ...member, role: this.roleDraft });
        // Exceptions stay exceptions; the boxes now start from the new role's defaults.
        this.resetDraft();
        this.personMessage.set(`Role changed to ${this.roleDraft}. It applies the next time they sign in.`);
      },
      error: (err) => {
        this.savingRole.set(false);
        this.roleDraft = member.role;
        this.personMessage.set(err?.error?.message ?? 'Could not change the role.');
      },
    });
  }

  toggleActive(): void {
    const member = this.member();
    if (!member) {
      return;
    }
    const next = !member.isActive;
    this.savingStatus.set(true);
    this.personMessage.set(null);
    this.identityApi.setActive(member.id, next).subscribe({
      next: () => {
        this.savingStatus.set(false);
        this.member.set({ ...member, isActive: next });
        this.personMessage.set(next ? `${member.displayName} can sign in again.` : `${member.displayName} can no longer sign in to any app.`);
      },
      error: (err) => {
        this.savingStatus.set(false);
        this.personMessage.set(err?.error?.message ?? 'Could not change this.');
      },
    });
  }

  // ---- helpers ----

  private update(change: (map: Map<string, boolean>) => void): void {
    if (!this.editable()) {
      return;
    }
    const next = new Map(this.draft());
    change(next);
    this.draft.set(next);
    this.savedMessage.set(null);
  }

  private resetDraft(): void {
    this.draft.set(this.buildDraft(this.stored()));
  }

  /** The boxes as the stored exceptions on top of the role's defaults would show them. */
  private buildDraft(stored: Record<string, boolean>): Map<string, boolean> {
    const draft = new Map(this.defaults());
    for (const [key, value] of Object.entries(stored)) {
      if (draft.has(key)) {
        draft.set(key, value);
      }
    }
    // Same rule as the apps: an area they can't view can't be changed either.
    for (const app of this.apps()) {
      for (const feature of app.features) {
        if (!draft.get(actionKey(app.service, feature.key, 'view'))) {
          for (const action of feature.actions) {
            draft.set(actionKey(app.service, feature.key, action.key), false);
          }
        }
      }
    }
    return draft;
  }

  /**
   * What differs from the role's defaults. Add/Edit/Delete of an area that can't be viewed are implied by
   * the View box, so they are left out rather than stored.
   */
  private exceptionsOf(draft: Map<string, boolean>): Record<string, boolean> {
    const result: Record<string, boolean> = {};
    const defaults = this.defaults();
    for (const [key, value] of draft) {
      const parts = key.split(':');
      if (parts.length === 3 && parts[2] !== 'view' && !draft.get(`${parts[0]}:${parts[1]}:view`)) {
        continue;
      }
      if (defaults.get(key) !== value) {
        result[key] = value;
      }
    }
    return result;
  }
}

function sameMap(a: Record<string, boolean>, b: Record<string, boolean>): boolean {
  const ak = Object.keys(a);
  return ak.length === Object.keys(b).length && ak.every((k) => b[k] === a[k]);
}
