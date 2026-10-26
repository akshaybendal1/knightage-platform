import { Component, OnInit, signal } from '@angular/core';
import { Auth } from '../../core/auth';
import { PlatformApi } from '../../core/platform-api';
import { PlanSummary, ServiceCatalogEntry } from '../../core/models';

const SERVICE_ICONS: Record<string, string> = {
  Crm: 'fa-users',
  Accounting: 'fa-calculator',
  InventorySales: 'fa-boxes-stacked',
};

@Component({
  selector: 'app-services',
  imports: [],
  templateUrl: './services.html',
  styleUrl: './services.css',
})
export class Services implements OnInit {
  services = signal<ServiceCatalogEntry[]>([]);
  fieldForceTrackingEnabled = signal(false);
  plan = signal<PlanSummary | null>(null);
  availablePlans = signal<PlanSummary[]>([]);
  loading = signal(false);
  errorMessage = signal<string | null>(null);

  // Which service keys (or 'fieldForceTracking') have an in-flight or already-sent
  // request this page load -- resets on reload, same lifetime as
  // knightage-crm's Organization page's own requestSent signal.
  requestingKeys = signal<Set<string>>(new Set());
  requestSentKeys = signal<Set<string>>(new Set());

  constructor(
    private readonly api: PlatformApi,
    private readonly auth: Auth,
  ) {}

  ngOnInit(): void {
    this.loading.set(true);
    this.api.getMyServices().subscribe({
      next: (result) => {
        this.services.set(result.services);
        this.fieldForceTrackingEnabled.set(result.fieldForceTrackingEnabled);
        this.plan.set(result.plan);
        this.availablePlans.set(result.availablePlans);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Could not load your services.');
        this.loading.set(false);
      },
    });
  }

  iconClass(key: string): string {
    return SERVICE_ICONS[key] ?? 'fa-cube';
  }

  open(service: ServiceCatalogEntry): void {
    if (!service.provisioned || !service.url || !this.auth.token) {
      return;
    }
    window.location.href = `${service.url}/auth-callback?token=${encodeURIComponent(this.auth.token)}`;
  }

  isRequesting(key: string): boolean {
    return this.requestingKeys().has(key);
  }

  isRequestSent(key: string): boolean {
    return this.requestSentKeys().has(key);
  }

  requestService(service: ServiceCatalogEntry): void {
    this.setRequesting(service.key, true);
    this.api.requestService(service.key).subscribe({
      next: () => {
        this.setRequesting(service.key, false);
        this.markRequestSent(service.key);
      },
      error: () => {
        this.setRequesting(service.key, false);
        this.errorMessage.set(`Could not send the request for ${service.displayName}.`);
      },
    });
  }

  requestFieldForceTracking(): void {
    const key = 'fieldForceTracking';
    this.setRequesting(key, true);
    this.api.requestFieldForceTracking().subscribe({
      next: () => {
        this.setRequesting(key, false);
        this.markRequestSent(key);
      },
      error: () => {
        this.setRequesting(key, false);
        this.errorMessage.set('Could not send the request for Field Force Tracking.');
      },
    });
  }

  requestPlan(planId: string): void {
    const key = `plan:${planId}`;
    this.setRequesting(key, true);
    this.api.requestPlanChange(planId).subscribe({
      next: () => {
        this.setRequesting(key, false);
        this.markRequestSent(key);
      },
      error: () => {
        this.setRequesting(key, false);
        this.errorMessage.set('Could not send the plan request.');
      },
    });
  }

  private setRequesting(key: string, requesting: boolean): void {
    this.requestingKeys.update((keys) => {
      const next = new Set(keys);
      if (requesting) {
        next.add(key);
      } else {
        next.delete(key);
      }
      return next;
    });
  }

  private markRequestSent(key: string): void {
    this.requestSentKeys.update((keys) => new Set(keys).add(key));
  }
}
