-- Per-user access to the Knightage apps, managed by an organization's Owner / Admins on Platform's
-- Users screen: which apps a user may open (AccessKey 'Crm', 'InventorySales', 'Accounting') and,
-- inside an app, what they may do in each area (AccessKey 'Crm:leads:add', 'Accounting:vouchers:view', ...).
--
-- Only exceptions are stored. With no row a user gets exactly what their role gave them before this
-- table existed (every app; in each app the role's defaults -- in CRM including the organization's
-- Access Control settings), so an organization that never opens the screen sees no change.
--
-- Additive only: a new table, nothing existing is touched. Safe to run more than once.

IF OBJECT_ID('UserAccessOverrides', 'U') IS NULL
BEGIN
    CREATE TABLE UserAccessOverrides (
        OrganizationId  UNIQUEIDENTIFIER NOT NULL,
        UserId          UNIQUEIDENTIFIER NOT NULL,
        AccessKey       NVARCHAR(150)    NOT NULL,
        IsAllowed       BIT              NOT NULL,
        UpdatedAtUtc    DATETIME2        NOT NULL,
        UpdatedByUserId UNIQUEIDENTIFIER NULL,
        CONSTRAINT PK_UserAccessOverrides PRIMARY KEY (OrganizationId, UserId, AccessKey)
    );
END
