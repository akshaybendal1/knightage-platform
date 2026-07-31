-- Bootstrap schema for local/dev use.
-- Once knightage-platform's migration orchestration exists, this becomes the
-- versioned source it applies instead of being run by hand.

CREATE TABLE Tenants (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    OrganizationId UNIQUEIDENTIFIER NOT NULL UNIQUE,
    Name NVARCHAR(200) NOT NULL,
    Slug NVARCHAR(100) NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Active',
    CreatedAtUtc DATETIME2 NOT NULL
);

CREATE TABLE TenantServiceDatabases (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TenantId UNIQUEIDENTIFIER NOT NULL REFERENCES Tenants(Id),
    ServiceName NVARCHAR(50) NOT NULL,
    DatabaseName NVARCHAR(128) NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Provisioned',
    CreatedAtUtc DATETIME2 NOT NULL
);
