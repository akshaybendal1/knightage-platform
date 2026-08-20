-- Bootstrap schema for local/dev use.
-- Once knightage-platform's migration orchestration exists, this becomes the
-- versioned source it applies instead of being run by hand.

CREATE TABLE PipelineStages (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(100) NOT NULL,
    SortOrder INT NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE Leads (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    Email NVARCHAR(320) NULL,
    Phone NVARCHAR(50) NULL,
    Company NVARCHAR(200) NULL,
    PipelineStageId UNIQUEIDENTIFIER NOT NULL REFERENCES PipelineStages(Id),
    Notes NVARCHAR(1000) NULL,
    Source NVARCHAR(50) NOT NULL DEFAULT 'Manual',
    CreatedAtUtc DATETIME2 NOT NULL
);

CREATE TABLE LeadActivities (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    LeadId UNIQUEIDENTIFIER NOT NULL REFERENCES Leads(Id),
    Type NVARCHAR(50) NOT NULL DEFAULT 'Note',
    Content NVARCHAR(2000) NOT NULL,
    Metadata NVARCHAR(MAX) NULL,
    CreatedByUserId NVARCHAR(100) NULL,
    CreatedAtUtc DATETIME2 NOT NULL
);

CREATE INDEX IX_LeadActivities_LeadId ON LeadActivities(LeadId);
