export interface Tenant {
  id: string;
  organizationId: string;
  name: string;
  slug: string;
  status: string;
  createdAtUtc: string;
  accentColor: string | null;
  fieldForceTrackingEnabled: boolean;
  accountingMultiCompanyEnabled: boolean;
  planId: string | null;
  isMigrating: boolean;
}

export interface Plan {
  id: string;
  name: string;
  maxUsers: number | null;
  maxRecords: number | null;
  priceMonthly: number | null;
  isActive: boolean;
  sortOrder: number;
  createdAtUtc: string;
}

export interface PlanSummary {
  id: string;
  name: string;
  maxUsers: number | null;
  maxRecords: number | null;
  priceMonthly: number | null;
}

export interface TenantServiceDatabase {
  id: string;
  tenantId: string;
  serviceName: string;
  // Null means the default/shared server for this service -- only set once this tenant's
  // data has moved to a database on a different SQL Server.
  serverAddress: string | null;
  databaseName: string;
  status: string;
  createdAtUtc: string;
}

export interface TenantProvisioningResult {
  tenant: Tenant;
  databases: TenantServiceDatabase[];
}

// 'NotSubscribed' is what the API reports for a tenant that has no subscription row at all.
export type SubscriptionStatus = 'Active' | 'ReadOnly' | 'Disabled' | 'NotSubscribed';

export type TenantSortKey = 'name' | 'slug' | 'plan' | 'subscription' | 'ends' | 'created';

// Everything is optional: an empty filter set is the plain newest-first list.
export interface TenantListFilters {
  search?: string;
  /** A plan id, or 'none' for tenants with no plan. */
  plan?: string;
  subscriptionStatus?: SubscriptionStatus;
  sortBy?: TenantSortKey;
  sortDir?: 'asc' | 'desc';
}

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
  plan: PlanSummary | null;
}

export interface PagedTenants {
  items: TenantListItem[];
  totalCount: number;
}

export interface TenantDetail {
  tenant: Tenant;
  databases: TenantServiceDatabase[];
  subscription: Subscription;
  plan: PlanSummary | null;
  crmMigrationAwaitingPurge: boolean;
}

export interface CrmMigrationResult {
  targetServer: string | null;
  targetDatabaseName: string;
  rowCounts: Record<string, number>;
}

export interface ServiceCatalogEntry {
  key: string;
  displayName: string;
  url: string | null;
  provisioned: boolean;
  /** False when an administrator has taken this app away from the signed-in user (Users screen). */
  userAllowed: boolean;
}

export interface MyServicesResponse {
  services: ServiceCatalogEntry[];
  fieldForceTrackingEnabled: boolean;
  plan: PlanSummary | null;
  availablePlans: PlanSummary[];
}

export interface AuthResult {
  token: string;
  expiresAtUtc: string;
  /** Seconds the token has left by Identity's clock (absent on a token handed over via ?token=). */
  expiresInSeconds?: number;
  userId: string;
  organizationId: string;
  email: string;
  displayName: string;
  role: string;
  /** Traded for the next token before this one expires (absent on a token handed over via ?token=). */
  refreshToken?: string | null;
  refreshTokenExpiresAtUtc?: string | null;
}

// ---- Users screen: people (knightage-identity) and their access to the apps (this app) ----

export type ConfigurableRole = 'Admin' | 'Member' | 'Sales';

export interface TeamMember {
  id: string;
  displayName: string;
  email: string;
  role: string;
  isActive: boolean;
  createdAtUtc: string;
  phoneNumber: string | null;
}

export interface AccessActionDef {
  key: 'view' | 'add' | 'edit' | 'delete';
  label: string;
}

export interface AccessFeatureDef {
  key: string;
  label: string;
  group: string;
  actions: AccessActionDef[];
  /** What each role may do here when nothing is customised: every listed action, or none. */
  defaults: Record<ConfigurableRole, boolean>;
}

export interface AccessAppDef {
  service: string;
  displayName: string;
  /** The organization has this app (provisioned and not switched off by Knightage). */
  enabled: boolean;
  /** CRM's role defaults come from its Access Control screen when it could be read. */
  roleDefaultsFrom: 'crm-access-control' | 'built-in';
  features: AccessFeatureDef[];
}

export interface AccessCatalog {
  roles: ConfigurableRole[];
  apps: AccessAppDef[];
}

export interface UserAccessSummary {
  userId: string;
  apps: Record<string, boolean>;
  areaExceptions: number;
  updatedAtUtc: string;
}

export interface UserAccess {
  userId: string;
  /** Only the exceptions: "Crm" -> may open CRM, "Crm:leads:add" -> may add leads. */
  overrides: Record<string, boolean>;
  updatedAtUtc: string | null;
  updatedByUserId: string | null;
}
