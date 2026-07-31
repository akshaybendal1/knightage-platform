-- Bootstrap schema for local/dev use.
-- Once knightage-platform's migration orchestration exists, this becomes the
-- versioned source it applies instead of being run by hand.

CREATE TABLE Accounts (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(500) NULL,
    SortId INT NOT NULL,
    Head NVARCHAR(50) NOT NULL
);

CREATE TABLE AccountingRules (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Category NVARCHAR(100) NOT NULL,
    DebitAccountId UNIQUEIDENTIFIER NOT NULL REFERENCES Accounts(Id),
    CreditAccountId UNIQUEIDENTIFIER NOT NULL REFERENCES Accounts(Id),
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE DraftEntries (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TransactionDate DATETIME2 NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    Category NVARCHAR(100) NOT NULL,
    Narration NVARCHAR(500) NULL,
    Source NVARCHAR(50) NOT NULL DEFAULT 'Manual',
    Status NVARCHAR(20) NOT NULL DEFAULT 'Pending',
    VendorName NVARCHAR(200) NULL,
    InvoiceNumber NVARCHAR(100) NULL,
    ExtractionConfidence FLOAT NULL,
    CreatedAtUtc DATETIME2 NOT NULL
);

CREATE TABLE JournalEntries (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    TransactionDate DATETIME2 NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    DebitAccountId UNIQUEIDENTIFIER NOT NULL REFERENCES Accounts(Id),
    CreditAccountId UNIQUEIDENTIFIER NOT NULL REFERENCES Accounts(Id),
    DraftEntryId UNIQUEIDENTIFIER NULL REFERENCES DraftEntries(Id),
    Narration NVARCHAR(500) NULL,
    CreatedAtUtc DATETIME2 NOT NULL
);

CREATE TABLE BankTransactions (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    BankAccountId UNIQUEIDENTIFIER NOT NULL REFERENCES Accounts(Id),
    ImportBatchId UNIQUEIDENTIFIER NOT NULL,
    TransactionDate DATETIME2 NOT NULL,
    Description NVARCHAR(500) NULL,
    Amount DECIMAL(18,2) NOT NULL,
    Status NVARCHAR(20) NOT NULL DEFAULT 'Unmatched',
    MatchedJournalEntryId UNIQUEIDENTIFIER NULL REFERENCES JournalEntries(Id),
    CreatedAtUtc DATETIME2 NOT NULL
);
