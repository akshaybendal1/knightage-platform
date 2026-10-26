-- Explicitly schema-qualified as dbo -- Platform's production DB runs migrations
-- under a login (appuser) whose own default schema is "appuser", not dbo, so an
-- unqualified CREATE TABLE here would land in the wrong schema and be invisible
-- to the app's runtime login (appuser_ab, default schema "appuser_ab"), which
-- only resolves unqualified names against its own default schema then dbo --
-- exactly the "Invalid object name" bug this caused in production on 2026-09-18.
-- Tenants/Subscriptions already live in dbo, which is why they never hit this.
CREATE TABLE dbo.Plans (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    MaxUsers INT NULL,
    MaxRecords INT NULL,
    PriceMonthly DECIMAL(10,2) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    SortOrder INT NOT NULL DEFAULT 0,
    CreatedAtUtc DATETIME2 NOT NULL
);

ALTER TABLE dbo.Tenants ADD PlanId UNIQUEIDENTIFIER NULL REFERENCES dbo.Plans(Id);

INSERT INTO dbo.Plans (Id, Name, MaxUsers, MaxRecords, PriceMonthly, IsActive, SortOrder, CreatedAtUtc) VALUES
(NEWID(), 'Basic', 5, 10000, 1499.00, 1, 1, GETUTCDATE()),
(NEWID(), 'Pro', 20, 100000, 4999.00, 1, 2, GETUTCDATE());
