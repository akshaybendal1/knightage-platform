-- Lets a PlatformAdmin allow a tenant to keep books for more than one company in
-- knightage-accounting (an accounting firm keeping its clients' books). Off by default:
-- an organization's Accounting holds just its own company, same on/off pattern as
-- FieldForceTrackingEnabled (004).

ALTER TABLE Tenants ADD AccountingMultiCompanyEnabled BIT NOT NULL DEFAULT 0;
