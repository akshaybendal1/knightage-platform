import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { CrmMigrationResult, MyServicesResponse, PagedTenants, Plan, Subscription, TenantDetail, TenantListFilters, TenantProvisioningResult } from './models';

@Injectable({
  providedIn: 'root',
})
export class PlatformApi {
  constructor(private readonly http: HttpClient) {}

  getTenants(page: number, pageSize: number, filters: TenantListFilters = {}) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    // Only send what's set, so an untouched filter doesn't show up as an empty query value.
    for (const [key, value] of Object.entries(filters)) {
      if (value !== undefined && value !== '') {
        params = params.set(key, value);
      }
    }
    return this.http.get<PagedTenants>('/api/tenants', { params });
  }

  getMyServices() {
    return this.http.get<MyServicesResponse>('/api/tenants/me/services');
  }

  requestService(key: string) {
    return this.http.post<{ message: string }>(`/api/tenants/me/services/${key}/request`, {});
  }

  requestFieldForceTracking() {
    return this.http.post<{ message: string }>('/api/tenants/me/field-force-tracking/request', {});
  }

  requestPlanChange(planId: string) {
    return this.http.post<{ message: string }>('/api/tenants/me/plan/request', { planId });
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

  updateAccentColor(organizationId: string, accentColor: string | null) {
    return this.http.post<void>(`/api/tenants/${organizationId}/accent-color`, { accentColor });
  }

  updateFieldForceTracking(organizationId: string, enabled: boolean) {
    return this.http.post<void>(`/api/tenants/${organizationId}/field-force-tracking`, { enabled });
  }

  updateAccountingMultiCompany(organizationId: string, enabled: boolean) {
    return this.http.post<void>(`/api/tenants/${organizationId}/accounting-multi-company`, { enabled });
  }

  updateServiceStatus(organizationId: string, serviceKey: string, enabled: boolean) {
    return this.http.post<void>(`/api/tenants/${organizationId}/services/${serviceKey}/status`, { enabled });
  }

  assignPlan(organizationId: string, planId: string | null) {
    return this.http.post<void>(`/api/tenants/${organizationId}/plan`, { planId });
  }

  // CRM caches each org's subscription/plan/routing details for 15 minutes -- this tells it
  // to drop that cached entry now, so a subscription/plan change just made here shows up on
  // CRM's very next request instead of waiting out the cache.
  invalidateCrmCache(organizationId: string) {
    return this.http.post<void>(`/api/tenants/${organizationId}/crm-cache/invalidate`, {});
  }

  getPlans() {
    return this.http.get<Plan[]>('/api/plans');
  }

  createPlan(payload: { name: string; maxUsers: number | null; maxRecords: number | null; priceMonthly: number | null; sortOrder: number }) {
    return this.http.post<Plan>('/api/plans', payload);
  }

  updatePlan(id: string, payload: { name: string; maxUsers: number | null; maxRecords: number | null; priceMonthly: number | null; sortOrder: number }) {
    return this.http.post<Plan>(`/api/plans/${id}`, payload);
  }

  setPlanStatus(id: string, isActive: boolean) {
    return this.http.post<void>(`/api/plans/${id}/status`, { isActive });
  }

  exportCrmData(organizationId: string) {
    return this.http.get(`/api/tenants/${organizationId}/migration/crm/export`, { responseType: 'blob' });
  }

  exportFullBundle(organizationId: string) {
    return this.http.get(`/api/tenants/${organizationId}/migration/export-bundle`, { responseType: 'blob' });
  }

  moveCrmData(organizationId: string, targetServer: string, targetDatabaseName: string) {
    return this.http.post<CrmMigrationResult>(`/api/tenants/${organizationId}/migration/crm`, {
      targetServer: targetServer.trim() || null,
      targetDatabaseName: targetDatabaseName.trim(),
    });
  }

  downloadCrmMigrationBackup(organizationId: string) {
    return this.http.get(`/api/tenants/${organizationId}/migration/crm/backup`, { responseType: 'blob' });
  }

  purgeCrmSharedData(organizationId: string) {
    return this.http.post<void>(`/api/tenants/${organizationId}/migration/crm/purge`, {});
  }
}
