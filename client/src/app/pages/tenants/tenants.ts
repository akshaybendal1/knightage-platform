import { Component, OnDestroy, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SlicePipe } from '@angular/common';
import { Subscription as RxSubscription } from 'rxjs';
import { PlatformApi } from '../../core/platform-api';
import { CrmMigrationResult, Plan, SubscriptionStatus, Tenant, TenantDetail, TenantListFilters, TenantListItem, TenantServiceDatabase, TenantSortKey } from '../../core/models';

// Mirrors Knightage.Platform.Core.ServiceCatalog.
const SERVICE_CATALOG = [
  { key: 'Crm', displayName: 'CRM' },
  { key: 'Accounting', displayName: 'Accounting' },
  { key: 'InventorySales', displayName: 'Inventory' },
];

@Component({
  selector: 'app-tenants',
  imports: [FormsModule, SlicePipe],
  templateUrl: './tenants.html',
  styleUrl: './tenants.css',
})
export class Tenants implements OnInit, OnDestroy {
  tenants = signal<TenantListItem[]>([]);
  readonly pageSize = 20;
  page = signal(1);
  totalCount = signal(0);
  totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));
  pageStart = computed(() => (this.totalCount() === 0 ? 0 : (this.page() - 1) * this.pageSize + 1));
  pageEnd = computed(() => Math.min(this.page() * this.pageSize, this.totalCount()));
  loading = signal(false);
  // Until the first response lands there is nothing to dim, so the grid shows a plain
  // "Loading..." instead; after that, reloads (typing in search, changing a filter) keep the
  // current rows on screen rather than flashing the whole table away on every keystroke.
  loadedOnce = signal(false);
  errorMessage = signal<string | null>(null);
  provisioning = signal(false);
  selectedDetail = signal<TenantDetail | null>(null);
  // One row per business app (same keys as the API's ServiceCatalog), including apps this
  // tenant has no row for yet -- "Enable" on those creates the row.
  serviceRows = computed(() => {
    const databases = this.selectedDetail()?.databases ?? [];
    return SERVICE_CATALOG.map((service) => ({
      ...service,
      db: databases.find((d) => d.serviceName.toLowerCase() === service.key.toLowerCase()) ?? null,
    }));
  });

  // Search / filter / sort. The API applies these (the grid is paged on the server, so
  // filtering only the rows on screen would miss every other page). The default is the order
  // the grid always had: newest first.
  search = '';
  planFilter = '';
  subscriptionFilter: '' | SubscriptionStatus = '';
  sortBy = signal<TenantSortKey>('created');
  sortDir = signal<'asc' | 'desc'>('desc');

  readonly subscriptionOptions: { value: SubscriptionStatus; label: string }[] = [
    { value: 'Active', label: 'Active' },
    { value: 'ReadOnly', label: 'Read-only (grace period)' },
    { value: 'Disabled', label: 'Disabled' },
    { value: 'NotSubscribed', label: 'Not subscribed' },
  ];
  readonly sortOptions: { key: TenantSortKey; label: string }[] = [
    { key: 'name', label: 'Name' },
    { key: 'slug', label: 'Slug' },
    { key: 'plan', label: 'Plan' },
    { key: 'subscription', label: 'Subscription' },
    { key: 'ends', label: 'Ends' },
    { key: 'created', label: 'Created' },
  ];

  // Search waits for a pause in typing before asking the API; a request still in flight when
  // the next one starts is cancelled so a slow, older answer can't overwrite a newer one.
  private searchTimer: ReturnType<typeof setTimeout> | undefined;
  private tenantsRequest: RxSubscription | undefined;
  private readonly loadErrorMessage = 'Could not load tenants.';

  organizationId = '';
  organizationName = '';

  // Subscription edit form, shown inline in the detail view.
  editingSubscription = signal(false);
  savingSubscription = signal(false);
  subStartDate = '';
  subEndDate = '';
  subGracePeriodDays = 7;
  subNotes = '';

  // Accent color edit form, shown inline in the detail view.
  editingAccentColor = signal(false);
  savingAccentColor = signal(false);
  accentColorInput = '#e66239';

  savingFieldForceTracking = signal(false);
  savingAccountingMultiCompany = signal(false);

  // Holds the serviceName currently saving (not a plain bool) so only that row's
  // button shows a loading state -- the Service databases table can have several
  // rows, each independently toggleable.
  savingServiceStatus = signal<string | null>(null);

  plans = signal<Plan[]>([]);
  savingPlan = signal(false);
  selectedPlanId = '';

  // Best-effort nudge for CRM's subscription/plan cache -- see invalidateCrmCache().
  syncingCrmCache = signal(false);
  crmCacheSynced = signal(false);

  // "Scaling" section -- move a tenant's CRM data to its own dedicated database, or
  // export it (alone, or bundled with Identity/Platform) for standing up elsewhere.
  exportingCrm = signal(false);
  exportingBundle = signal(false);
  migratingCrm = signal(false);
  migrationResult = signal<CrmMigrationResult | null>(null);
  purgingCrm = signal(false);

  constructor(private readonly api: PlatformApi) {}

  subscriptionBadgeClass(status: SubscriptionStatus): string {
    switch (status) {
      case 'Active':
        return 'badge-success';
      case 'ReadOnly':
        return 'badge-warning';
      case 'Disabled':
        return 'badge-danger';
      default:
        // The API can return statuses (e.g. "NotSubscribed") outside this
        // frontend's SubscriptionStatus union -- fall back to a neutral
        // badge instead of rendering unstyled.
        return 'badge-secondary';
    }
  }

  ngOnInit(): void {
    this.loadTenants();
    this.api.getPlans().subscribe((plans) => this.plans.set(plans));
  }

  ngOnDestroy(): void {
    clearTimeout(this.searchTimer);
    this.tenantsRequest?.unsubscribe();
  }

  loadTenants(): void {
    this.tenantsRequest?.unsubscribe();
    this.loading.set(true);
    this.tenantsRequest = this.api.getTenants(this.page(), this.pageSize, this.currentFilters()).subscribe({
      next: (result) => {
        this.tenants.set(result.items);
        this.totalCount.set(result.totalCount);
        // If the data shrank under the page being viewed (e.g. a narrower filter applied from
        // a later page), step back to the last page that exists instead of showing it empty.
        const lastPage = Math.max(1, Math.ceil(result.totalCount / this.pageSize));
        if (this.page() > lastPage) {
          this.page.set(lastPage);
          this.loadTenants();
          return;
        }
        if (this.errorMessage() === this.loadErrorMessage) {
          this.errorMessage.set(null);
        }
        this.loading.set(false);
        this.loadedOnce.set(true);
      },
      error: () => {
        this.errorMessage.set(this.loadErrorMessage);
        this.loading.set(false);
        this.loadedOnce.set(true);
      },
    });
  }

  private currentFilters(): TenantListFilters {
    return {
      search: this.search.trim(),
      plan: this.planFilter,
      subscriptionStatus: this.subscriptionFilter || undefined,
      sortBy: this.sortBy(),
      sortDir: this.sortDir(),
    };
  }

  hasActiveFilters(): boolean {
    return this.search.trim() !== '' || this.planFilter !== '' || this.subscriptionFilter !== '';
  }

  // Any change to what is searched or filtered starts again from the first page.
  applyFilters(): void {
    clearTimeout(this.searchTimer);
    this.page.set(1);
    this.loadTenants();
  }

  onSearchInput(): void {
    clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.applyFilters(), 300);
  }

  clearFilters(): void {
    this.search = '';
    this.planFilter = '';
    this.subscriptionFilter = '';
    this.applyFilters();
  }

  // Clicking the column that is already the sort flips its direction; a new column starts
  // ascending, except Created, where newest-first is the useful way round.
  setSort(key: TenantSortKey): void {
    if (this.sortBy() === key) {
      this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
    } else {
      this.sortBy.set(key);
      this.sortDir.set(key === 'created' ? 'desc' : 'asc');
    }
    this.page.set(1);
    this.loadTenants();
  }

  toggleSortDir(): void {
    this.sortDir.set(this.sortDir() === 'asc' ? 'desc' : 'asc');
    this.page.set(1);
    this.loadTenants();
  }

  sortIconClass(key: TenantSortKey): string {
    if (this.sortBy() !== key) {
      return 'fa-sort';
    }
    return this.sortDir() === 'asc' ? 'fa-sort-up' : 'fa-sort-down';
  }

  ariaSort(key: TenantSortKey): 'ascending' | 'descending' | 'none' {
    if (this.sortBy() !== key) {
      return 'none';
    }
    return this.sortDir() === 'asc' ? 'ascending' : 'descending';
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.page()) {
      return;
    }
    this.page.set(page);
    this.loadTenants();
  }

  provision(): void {
    this.errorMessage.set(null);
    if (!this.organizationId || !this.organizationName) {
      this.errorMessage.set('Enter an organization ID and name.');
      return;
    }

    this.provisioning.set(true);
    this.api.provisionTenant({ organizationId: this.organizationId, organizationName: this.organizationName }).subscribe({
      next: () => {
        this.provisioning.set(false);
        this.organizationId = '';
        this.organizationName = '';
        // A filter or sort could hide the tenant that was just provisioned, so show the
        // plain newest-first list.
        this.search = '';
        this.planFilter = '';
        this.subscriptionFilter = '';
        this.sortBy.set('created');
        this.sortDir.set('desc');
        this.applyFilters();
      },
      error: (err) => {
        this.provisioning.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Could not provision tenant.');
      },
    });
  }

  viewTenant(tenant: Tenant): void {
    this.editingSubscription.set(false);
    this.migrationResult.set(null);
    this.crmCacheSynced.set(false);
    this.api.getTenant(tenant.organizationId).subscribe((detail) => {
      this.selectedDetail.set(detail);
      this.selectedPlanId = detail.tenant.planId ?? '';
    });
  }

  closeDetail(): void {
    this.selectedDetail.set(null);
    this.editingSubscription.set(false);
    this.migrationResult.set(null);
    this.crmCacheSynced.set(false);
  }

  startEditSubscription(): void {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }
    this.subStartDate = detail.subscription.startDate?.slice(0, 10) ?? new Date().toISOString().slice(0, 10);
    this.subEndDate = detail.subscription.endDate?.slice(0, 10) ?? '';
    this.subGracePeriodDays = detail.subscription.gracePeriodDays ?? 7;
    this.subNotes = detail.subscription.notes ?? '';
    this.editingSubscription.set(true);
  }

  cancelEditSubscription(): void {
    this.editingSubscription.set(false);
  }

  saveSubscription(): void {
    const detail = this.selectedDetail();
    if (!detail || !this.subStartDate || !this.subEndDate) {
      this.errorMessage.set('Enter both a start date and an end date.');
      return;
    }

    this.savingSubscription.set(true);
    this.errorMessage.set(null);
    this.api
      .updateSubscription(detail.tenant.organizationId, {
        startDate: this.subStartDate,
        endDate: this.subEndDate,
        gracePeriodDays: this.subGracePeriodDays,
        notes: this.subNotes || undefined,
      })
      .subscribe({
        next: () => {
          this.savingSubscription.set(false);
          this.editingSubscription.set(false);
          this.viewTenant(detail.tenant);
          this.loadTenants();
          this.invalidateCrmCache(detail.tenant.organizationId, true);
        },
        error: (err) => {
          this.savingSubscription.set(false);
          this.errorMessage.set(err?.error?.message ?? 'Could not save the subscription.');
        },
      });
  }

  startEditAccentColor(): void {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }
    this.accentColorInput = detail.tenant.accentColor ?? '#e66239';
    this.editingAccentColor.set(true);
  }

  cancelEditAccentColor(): void {
    this.editingAccentColor.set(false);
  }

  saveAccentColor(): void {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }
    this.persistAccentColor(detail.tenant.organizationId, this.accentColorInput);
  }

  resetAccentColor(): void {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }
    this.persistAccentColor(detail.tenant.organizationId, null);
  }

  private persistAccentColor(organizationId: string, accentColor: string | null): void {
    this.savingAccentColor.set(true);
    this.errorMessage.set(null);
    this.api.updateAccentColor(organizationId, accentColor).subscribe({
      next: () => {
        this.savingAccentColor.set(false);
        this.editingAccentColor.set(false);
        this.api.getTenant(organizationId).subscribe((detail) => this.selectedDetail.set(detail));
        this.loadTenants();
      },
      error: (err) => {
        this.savingAccentColor.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Could not save the accent color.');
      },
    });
  }

  toggleFieldForceTracking(): void {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }
    const next = !detail.tenant.fieldForceTrackingEnabled;
    this.savingFieldForceTracking.set(true);
    this.errorMessage.set(null);
    this.api.updateFieldForceTracking(detail.tenant.organizationId, next).subscribe({
      next: () => {
        this.savingFieldForceTracking.set(false);
        this.api.getTenant(detail.tenant.organizationId).subscribe((updated) => this.selectedDetail.set(updated));
        this.loadTenants();
      },
      error: (err) => {
        this.savingFieldForceTracking.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Could not update Field Force Tracking.');
      },
    });
  }

  toggleAccountingMultiCompany(): void {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }
    const next = !detail.tenant.accountingMultiCompanyEnabled;
    this.savingAccountingMultiCompany.set(true);
    this.errorMessage.set(null);
    this.api.updateAccountingMultiCompany(detail.tenant.organizationId, next).subscribe({
      next: () => {
        this.savingAccountingMultiCompany.set(false);
        this.api.getTenant(detail.tenant.organizationId).subscribe((updated) => this.selectedDetail.set(updated));
      },
      error: (err) => {
        this.savingAccountingMultiCompany.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Could not update the Accounting companies setting.');
      },
    });
  }

  toggleServiceStatus(serviceKey: string, db: TenantServiceDatabase | null): void {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }
    const next = db?.status !== 'Provisioned';
    this.savingServiceStatus.set(serviceKey);
    this.errorMessage.set(null);
    this.api.updateServiceStatus(detail.tenant.organizationId, serviceKey, next).subscribe({
      next: () => {
        this.savingServiceStatus.set(null);
        this.api.getTenant(detail.tenant.organizationId).subscribe((updated) => this.selectedDetail.set(updated));
      },
      error: (err) => {
        this.savingServiceStatus.set(null);
        this.errorMessage.set(err?.error?.message ?? 'Could not update this service.');
      },
    });
  }

  saveTenantPlan(): void {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }
    this.savingPlan.set(true);
    this.errorMessage.set(null);
    this.api.assignPlan(detail.tenant.organizationId, this.selectedPlanId || null).subscribe({
      next: () => {
        this.savingPlan.set(false);
        this.api.getTenant(detail.tenant.organizationId).subscribe((updated) => {
          this.selectedDetail.set(updated);
          this.selectedPlanId = updated.tenant.planId ?? '';
        });
        this.loadTenants();
        this.invalidateCrmCache(detail.tenant.organizationId, true);
      },
      error: (err) => {
        this.savingPlan.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Could not assign the plan.');
      },
    });
  }

  // CRM caches each org's subscription/plan/routing details for up to 15 minutes -- this
  // tells it to drop that cached entry now, so a change just made here shows up on CRM's
  // very next request instead of the admin (or a customer) waiting out the cache or wondering
  // why nothing changed. Called automatically after saving a subscription or plan (silent,
  // best-effort -- the save itself already succeeded, this is just a nice-to-have on top of
  // it), and also exposed as its own button for any other case (e.g. syncing after a change
  // made outside this exact flow).
  invalidateCrmCache(organizationId: string, silent = false): void {
    if (!silent) {
      this.syncingCrmCache.set(true);
      this.crmCacheSynced.set(false);
      this.errorMessage.set(null);
    }
    this.api.invalidateCrmCache(organizationId).subscribe({
      next: () => {
        if (!silent) {
          this.syncingCrmCache.set(false);
          this.crmCacheSynced.set(true);
        }
      },
      error: (err) => {
        if (!silent) {
          this.syncingCrmCache.set(false);
          this.errorMessage.set(err?.error?.message ?? 'Could not sync CRM\'s cache.');
        }
      },
    });
  }

  // Just informational now -- moving is always allowed (a tenant can be rebalanced from one
  // shard to another more than once), so this only drives the "current location" display,
  // not whether the move form is shown.
  isCrmDedicated(detail: TenantDetail): boolean {
    const crmDatabase = detail.databases.find((db) => db.serviceName === 'Crm');
    return !!crmDatabase && crmDatabase.databaseName !== 'Knightage_Crm';
  }

  crmCurrentLocation(detail: TenantDetail): string {
    const crmDatabase = detail.databases.find((db) => db.serviceName === 'Crm');
    if (!crmDatabase) {
      return 'not provisioned';
    }
    return crmDatabase.serverAddress ? `${crmDatabase.serverAddress} / ${crmDatabase.databaseName}` : crmDatabase.databaseName;
  }

  // Move-target form, shown inline in the Scaling section. Target database must already
  // exist -- this app has no CREATE DATABASE rights and, with sharding, a target can
  // legitimately already hold other tenants' data, so "create a fresh one" isn't always
  // even the right action. Leaving the server blank means "the default shared server".
  moveTargetServer = '';
  moveTargetDatabaseName = '';

  exportCrmData(): void {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }
    this.exportingCrm.set(true);
    this.errorMessage.set(null);
    this.api.exportCrmData(detail.tenant.organizationId).subscribe({
      next: (blob) => {
        this.exportingCrm.set(false);
        downloadBlob(blob, `crm-export-${detail.tenant.organizationId}.sql`);
      },
      error: () => {
        this.exportingCrm.set(false);
        this.errorMessage.set('Could not export CRM data.');
      },
    });
  }

  exportFullBundle(): void {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }
    this.exportingBundle.set(true);
    this.errorMessage.set(null);
    this.api.exportFullBundle(detail.tenant.organizationId).subscribe({
      next: (blob) => {
        this.exportingBundle.set(false);
        downloadBlob(blob, `tenant-export-bundle-${detail.tenant.organizationId}.sql`);
      },
      error: () => {
        this.exportingBundle.set(false);
        this.errorMessage.set('Could not export the full tenant bundle.');
      },
    });
  }

  moveCrmData(): void {
    const detail = this.selectedDetail();
    if (!detail || !this.moveTargetDatabaseName.trim()) {
      return;
    }
    const target = this.moveTargetServer.trim()
      ? `${this.moveTargetServer.trim()} / ${this.moveTargetDatabaseName.trim()}`
      : this.moveTargetDatabaseName.trim();
    if (!confirm(`This puts "${detail.tenant.name}" into maintenance mode while its CRM data copies to ${target}. The target database must already exist and be reachable. Continue?`)) {
      return;
    }
    this.migratingCrm.set(true);
    this.errorMessage.set(null);
    this.migrationResult.set(null);
    this.api.moveCrmData(detail.tenant.organizationId, this.moveTargetServer, this.moveTargetDatabaseName).subscribe({
      next: (result) => {
        this.migratingCrm.set(false);
        this.migrationResult.set(result);
        this.moveTargetServer = '';
        this.moveTargetDatabaseName = '';
        this.viewTenant(detail.tenant);
        this.loadTenants();
      },
      error: (err) => {
        this.migratingCrm.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Could not move this tenant\'s CRM data.');
      },
    });
  }

  downloadCrmMigrationBackup(): void {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }
    this.api.downloadCrmMigrationBackup(detail.tenant.organizationId).subscribe({
      next: (blob) => downloadBlob(blob, `crm-backup-${detail.tenant.organizationId}.sql`),
      error: () => this.errorMessage.set('Could not download the backup.'),
    });
  }

  purgeCrmSharedData(): void {
    const detail = this.selectedDetail();
    if (!detail) {
      return;
    }
    if (!confirm(`This permanently deletes "${detail.tenant.name}"'s CRM data from the shared database. Make sure you've downloaded the backup first. Continue?`)) {
      return;
    }
    this.purgingCrm.set(true);
    this.errorMessage.set(null);
    this.api.purgeCrmSharedData(detail.tenant.organizationId).subscribe({
      next: () => {
        this.purgingCrm.set(false);
        this.migrationResult.set(null);
        this.viewTenant(detail.tenant);
      },
      error: (err) => {
        this.purgingCrm.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Could not clean up the old data.');
      },
    });
  }
}

function downloadBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}
