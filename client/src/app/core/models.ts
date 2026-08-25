export interface Tenant {
  id: string;
  organizationId: string;
  name: string;
  slug: string;
  status: string;
  createdAtUtc: string;
}

export interface TenantServiceDatabase {
  id: string;
  tenantId: string;
  serviceName: string;
  databaseName: string;
  status: string;
  createdAtUtc: string;
}

export interface TenantProvisioningResult {
  tenant: Tenant;
  databases: TenantServiceDatabase[];
}

export type SubscriptionStatus = 'Active' | 'ReadOnly' | 'Disabled';

export interface Subscription {
  status: SubscriptionStatus;
  startDate: string | null;
  endDate: string | null;
  gracePeriodDays: number | null;
  notes: string | null;
}

export interface TenantListItem {
  tenant: Tenant;
  subscription: Subscription;
}

export interface TenantDetail {
  tenant: Tenant;
  databases: TenantServiceDatabase[];
  subscription: Subscription;
}

export interface AuthResult {
  token: string;
  expiresAtUtc: string;
  userId: string;
  organizationId: string;
  email: string;
  displayName: string;
  role: string;
}
