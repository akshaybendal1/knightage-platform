import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SlicePipe } from '@angular/common';
import { PlatformApi } from '../../core/platform-api';
import { Tenant, TenantDetail } from '../../core/models';

@Component({
  selector: 'app-tenants',
  imports: [FormsModule, SlicePipe],
  templateUrl: './tenants.html',
  styleUrl: './tenants.css',
})
export class Tenants implements OnInit {
  tenants = signal<Tenant[]>([]);
  loading = signal(false);
  errorMessage = signal<string | null>(null);
  provisioning = signal(false);
  selectedDetail = signal<TenantDetail | null>(null);

  organizationId = '';
  organizationName = '';

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
    this.api.getTenant(tenant.organizationId).subscribe((detail) => this.selectedDetail.set(detail));
  }

  closeDetail(): void {
    this.selectedDetail.set(null);
  }
}
