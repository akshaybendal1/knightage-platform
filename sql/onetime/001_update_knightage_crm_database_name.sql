-- ONE-TIME cutover script, run against knightage-platform's own database in the same
-- maintenance window as knightage-crm/sql/onetime/001_migrate_knightage_tenant_data.sql.
-- Points the existing tenant's Crm bookkeeping row at the new shared database instead
-- of its old per-tenant one, so GET /api/tenants/{organizationId} (and this app's own
-- /tenants ops UI) reflect the new database name going forward.

UPDATE tsd
SET tsd.DatabaseName = 'Knightage_Crm'
FROM TenantServiceDatabases tsd
JOIN Tenants t ON t.Id = tsd.TenantId
WHERE t.OrganizationId = '578bceef-8dc7-4d8e-a1e4-39425a663841'
  AND tsd.ServiceName = 'Crm';
