-- PRODUCTION copy of 009_user_access_overrides.sql. On the production server knightage-platform's tables live in
-- the dbo schema (Tenants, TenantServiceDatabases, ...), while the deploy login's default schema is appuser: an
-- unqualified CREATE TABLE would land in appuser and a login with another default schema would not find it.
-- Creating it in dbo puts it next to the other tables, where every login resolves an unqualified name, and the
-- app logins' db_datareader / db_datawriter memberships already cover it.
--
-- Per-user access to the Knightage apps (Platform -> Users). Only exceptions to the role defaults are stored.
-- Additive only: a new table, nothing existing is touched. Safe to run more than once.

IF OBJECT_ID('dbo.UserAccessOverrides', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.UserAccessOverrides (
        OrganizationId  UNIQUEIDENTIFIER NOT NULL,
        UserId          UNIQUEIDENTIFIER NOT NULL,
        AccessKey       NVARCHAR(150)    NOT NULL,
        IsAllowed       BIT              NOT NULL,
        UpdatedAtUtc    DATETIME2        NOT NULL,
        UpdatedByUserId UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_UserAccessOverrides PRIMARY KEY (OrganizationId, UserId, AccessKey)
    );
END
