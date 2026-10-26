-- Lets a PlatformAdmin turn knightage-crm's Field Force Tracking module on/off
-- per tenant, the same on/off switch pattern as everything else gated from
-- here. Defaults to off -- existing tenants don't suddenly get a new role/menu
-- appearing until a PlatformAdmin opts them in from /tenants.

ALTER TABLE Tenants ADD FieldForceTrackingEnabled BIT NOT NULL DEFAULT 0;
