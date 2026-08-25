-- One subscription record per tenant. Absence of a row means "no
-- subscription set yet" -- treated as Active/unrestricted (see
-- SubscriptionStatusCalculator), so existing tenants aren't locked out the
-- moment this ships. A super user sets real dates going forward.

CREATE TABLE Subscriptions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL REFERENCES Tenants(Id),
    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,
    GracePeriodDays INT NOT NULL DEFAULT 7,
    Notes NVARCHAR(500) NULL,
    CreatedAtUtc DATETIME2 NOT NULL,
    UpdatedAtUtc DATETIME2 NOT NULL
);

CREATE UNIQUE INDEX IX_Subscriptions_TenantId ON Subscriptions(TenantId);
