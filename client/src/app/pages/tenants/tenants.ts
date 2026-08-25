import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SlicePipe } from '@angular/common';
import { PlatformApi } from '../../core/platform-api';
import { Tenant, TenantDetail, TenantListItem } from '../../core/models';

@Component({
  selector: 'app-tenants',
  imports: [FormsModule, SlicePipe],
  templateUrl: './tenants.html',
  styleUrl: './tenants.css',
})
export class Tenants implements OnInit {
  tenants = signal<TenantListItem[]>([]);
  loading = signal(false);
  errorMessage = signal<string | null>(null);
  provisioning = signal(false);
  selectedDetail = signal<TenantDetail | null>(null);

  organizationId = '';
  organizationName = '';

  // Subscription edit form, shown inline in the detail view.
  editingSubscription = signal(false);
  savingSubscription = signal(false);
  subStartDate = '';
  subEndDate = '';
  subGracePeriodDays = 7;
  subNotes = '';

  constructor(private readonly api: PlatformApi) {}

  ngOnInit(): void {
    this.loadTenants();
  }

  loadTenants(): void {
    this.loading.set(true);
    this.api.getTenants().subscribe({
      next: (tenants) => {
        this.tenants.set(tenants);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('Could not load tenants.');
        this.loading.set(false);
      },
    });
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
        this.loadTenants();
      },
      error: (err) => {
        this.provisioning.set(false);
        this.errorMessage.set(err?.error?.message ?? 'Could not provision tenant.');
      },
    });
  }

  viewTenant(tenant: Tenant): void {
    this.editingSubscription.set(false);
    this.api.getTenant(tenant.organizationId).subscribe((detail) => this.selectedDetail.set(detail));
  }

  closeDetail(): void {
    this.selectedDetail.set(null);
    this.editingSubscription.set(false);
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
        },
        error: (err) => {
          this.savingSubscription.set(false);
          this.errorMessage.set(err?.error?.message ?? 'Could not save the subscription.');
        },
      });
  }
}
