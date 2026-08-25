import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Subscription, TenantDetail, TenantListItem, TenantProvisioningResult } from './models';

@Injectable({
  providedIn: 'root',
})
export class PlatformApi {
  constructor(private readonly http: HttpClient) {}

  getTenants() {
    return this.http.get<TenantListItem[]>('/api/tenants');
  }

  getTenant(organizationId: string) {
    return this.http.get<TenantDetail>(`/api/tenants/${organizationId}`);
  }

  provisionTenant(payload: { organizationId: string; organizationName: string }) {
    return this.http.post<TenantProvisioningResult>('/api/tenants/provision', payload);
  }

  updateSubscription(organizationId: string, payload: { startDate: string; endDate: string; gracePeriodDays: number; notes?: string }) {
    return this.http.post<Subscription>(`/api/vendors/${organizationId}/subscription`, payload);
  }
}
