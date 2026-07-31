-- Bootstrap schema for local/dev use.
-- Once knightage-platform's migration orchestration exists, this becomes the
-- versioned source it applies instead of being run by hand.

CREATE TABLE Products (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Sku NVARCHAR(50) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(500) NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    QuantityOnHand INT NOT NULL DEFAULT 0
);

CREATE TABLE SalesOrders (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    OrderNumber NVARCHAR(50) NOT NULL,
    CustomerName NVARCHAR(200) NOT NULL,
    OrderDate DATETIME2 NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Confirmed',
    Total DECIMAL(18,2) NOT NULL,
    CreatedAtUtc DATETIME2 NOT NULL
);

CREATE TABLE SalesOrderLines (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    SalesOrderId UNIQUEIDENTIFIER NOT NULL REFERENCES SalesOrders(Id),
    ProductId UNIQUEIDENTIFIER NOT NULL REFERENCES Products(Id),
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(18,2) NOT NULL,
    LineTotal DECIMAL(18,2) NOT NULL
);
