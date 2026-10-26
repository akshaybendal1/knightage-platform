-- Lets a tenant's service database live on a different SQL Server than the default shared
-- one, and lets a target database already hold other tenants (sharding: "move 5 tenants to
-- server/DB A, 5 to server/DB B") rather than assuming every migration target is a brand
-- new, single-tenant database created by this app. NULL ServerAddress means "the default
-- server for this service" (CrmShared's own server today) -- every existing row stays NULL,
-- unchanged in meaning.
ALTER TABLE dbo.TenantServiceDatabases ADD ServerAddress NVARCHAR(200) NULL;

-- Same NULL convention, recorded per migration for an accurate audit trail of where a
-- tenant's data actually moved from/to.
ALTER TABLE dbo.TenantMigrations ADD OldServerAddress NVARCHAR(200) NULL;
ALTER TABLE dbo.TenantMigrations ADD NewServerAddress NVARCHAR(200) NULL;
