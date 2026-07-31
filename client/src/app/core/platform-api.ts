import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Tenant, TenantDetail, TenantProvisioningResult } from './models';

@Injectable({
  providedIn: 'root',
})
export class PlatformApi {
  constructor(private readonly http: HttpClient) {}

  getTenants() {
    return this.http.get<Tenant[]>('/api/tenants');
  }

  getTenant(organizationId: string) {
    return this.http.get<TenantDetail>(`/api/tenants/${organizationId}`);
  }

  provisionTenant(payload: { organizationId: string; organizationName: string }) {
    return this.http.post<TenantProvisioningResult>('/api/tenants/provision', payload);
  }
}
