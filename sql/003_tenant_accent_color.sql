-- Lets a PlatformAdmin set a per-tenant brand/accent color, applied in
-- knightage-crm's UI. NULL means "use the app's default color", not broken.
-- NVARCHAR(7) matches a #RRGGBB hex string exactly.

ALTER TABLE Tenants ADD AccentColor NVARCHAR(7) NULL;
