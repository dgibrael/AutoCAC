Drop TABLE if exists  dbo.Notification;
Drop TABLE if exists dbo.ProcessState;
Drop TABLE if exists  dbo.AlertMessage;
Drop TABLE if exists  dbo.Alert;
Drop TABLE if exists  dbo.AlertDefRuleNode;
Drop TABLE if exists  dbo.AlertDef;
Drop TABLE if exists  dbo.ClinicalFactMetadataMatch;
Drop TABLE if exists  dbo.ClinicalFact;
Drop TABLE if exists  dbo.ClinicalDefinitionMetadataMatch;
Drop TABLE if exists  dbo.ClinicalDefinitionItem;
Drop TABLE if exists  dbo.ClinicalDefinition;
GO

CREATE TABLE dbo.ClinicalDefinition (
    Id int IDENTITY(1,1) PRIMARY KEY NOT NULL,
    Name varchar(100) NOT NULL,
    Description nvarchar(1000) NULL,
    DataType varchar(50) NOT NULL,     -- Drug, Loinc, PharmacyOrderableItem, etc.
    IsActive bit NOT NULL DEFAULT 1,
    CONSTRAINT UQ_ClinicalDefinition_Name UNIQUE (Name)
);
GO

CREATE TABLE dbo.ClinicalDefinitionItem (
    Id int IDENTITY(1,1) PRIMARY KEY NOT NULL,
    ClinicalDefinitionId int NOT NULL,
    FieldName varchar(100) NOT NULL,   -- GenericName, LongName, ShortName, etc.
    Pattern nvarchar(200) NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CONSTRAINT FK_ClinicalDefinitionItem_ClinicalDefinition FOREIGN KEY (ClinicalDefinitionId)
        REFERENCES dbo.ClinicalDefinition(Id) ON DELETE CASCADE
);
GO

CREATE INDEX IX_ClinicalDefinitionItem_ClinicalDefinitionId
    ON dbo.ClinicalDefinitionItem(ClinicalDefinitionId);
GO

CREATE TABLE dbo.ClinicalDefinitionMetadataMatch (
    Id int IDENTITY(1,1) PRIMARY KEY NOT NULL,
    ClinicalDefinitionId int NOT NULL,
    DataType varchar(50) NOT NULL,
    MetadataRecordId varchar(200) NOT NULL,
    LastBuiltAt datetime2(2) NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_ClinicalDefinitionMetadataMatch_ClinicalDefinition FOREIGN KEY (ClinicalDefinitionId)
        REFERENCES dbo.ClinicalDefinition(Id) ON DELETE CASCADE,
    CONSTRAINT UQ_ClinicalDefinitionMetadataMatch UNIQUE (DataType, MetadataRecordId, ClinicalDefinitionId)
);
GO

CREATE TABLE dbo.ClinicalFact (
    Id bigint IDENTITY(1,1) PRIMARY KEY NOT NULL,
    DataType varchar(50) NOT NULL,
    TableName varchar(50) NULL,
    PatientId int not NULL,
    RecordKey varchar(100) NOT NULL,
    MetadataRecordId varchar(100) NOT NULL,
    EffectiveAt datetime2(2) NOT NULL DEFAULT SYSDATETIME(),
    ExpiresAt datetime2(2) NULL,
    IsActive bit NOT NULL DEFAULT 1,
    LastImportAt datetime2(2) NOT NULL DEFAULT SYSDATETIME(),
    NeedsProcessing bit NOT NULL DEFAULT 1,
    ValuesJson nvarchar(max) NULL
);
GO
ALTER TABLE [dbo].[ClinicalFact]  WITH NOCHECK ADD  CONSTRAINT [FK_ClinicalFact_PatientId] FOREIGN KEY([PatientId])
REFERENCES [dbo].[Patient] ([id])
GO
ALTER TABLE [dbo].[ClinicalFact] NOCHECK CONSTRAINT [FK_ClinicalFact_PatientId]
GO
CREATE INDEX IX_ClinicalFact_Pending
    ON dbo.ClinicalFact(DataType, MetadataRecordId, PatientId)
    WHERE NeedsProcessing = 1;
GO
CREATE INDEX IX_ClinicalFact_Active
    ON dbo.ClinicalFact(PatientId, DataType, MetadataRecordId)
    WHERE IsActive = 1;
GO

CREATE TABLE dbo.AlertDef(
    Id int IDENTITY(1,1) PRIMARY KEY NOT NULL,
    Name varchar(50) NOT NULL,
    Origin varchar(50) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    DisplayName VARCHAR(200) NOT NULL,
    Description varchar(1000) NULL,
    LastUpdated datetime2(2) NOT NULL DEFAULT SYSDATETIME(),
    UNIQUE (Name)
);
GO
CREATE TABLE dbo.AlertDefRuleNode (
    Id int IDENTITY(1,1) PRIMARY KEY NOT NULL,
    AlertDefId int NOT NULL,
    ParentId int NULL,
    NodeType varchar(100) NOT NULL,
    ClinicalDefinitionId int NULL,
    ChildOperator varchar(20) NULL,         -- used for Group rows
    FieldName varchar(100) NULL,
    FieldDataType varchar(20) NULL,
    Operator varchar(20) NULL,
    Value nvarchar(200) NULL,              
    IsActive bit NOT NULL DEFAULT 1,
    CONSTRAINT FK_AlertDefRuleNode_AlertDef FOREIGN KEY (AlertDefId)
        REFERENCES dbo.AlertDef(Id) ON DELETE CASCADE,
    CONSTRAINT FK_AlertDefRuleNode_Parent FOREIGN KEY (ParentId)
        REFERENCES dbo.AlertDefRuleNode(Id),
    CONSTRAINT FK_AlertDefRuleNode_ClinicalDefinition FOREIGN KEY (ClinicalDefinitionId)
        REFERENCES dbo.ClinicalDefinition(Id)
);
GO

CREATE INDEX IX_AlertDefRuleNode_AlertDefId_ParentId
    ON dbo.AlertDefRuleNode(AlertDefId, ParentId);
GO

CREATE TABLE dbo.Alert(
    Id int IDENTITY(1,1) PRIMARY KEY NOT NULL,
    CreatedAt datetime2(2) NOT NULL DEFAULT SYSDATETIME(),
    Title nvarchar(100) NOT NULL,
    HtmlBody nvarchar(max) NULL,
    AlertDefId int NOT NULL,
    auth_user_id int NULL,
    Status varchar(20) NOT NULL DEFAULT 'OPEN',
    LastActivityAt datetime2(2) NOT NULL DEFAULT SYSDATETIME(),
    IsPrivate bit NOT NULL DEFAULT 0,
    PatientId int null,
    EvidenceKey varchar(300) NULL,
    IsActive bit NOT NULL DEFAULT 1,
    FOREIGN KEY (AlertDefId) REFERENCES dbo.AlertDef(Id),
    FOREIGN KEY (auth_user_id) REFERENCES dbo.auth_user(id)
);
GO
ALTER TABLE [dbo].[Alert]  WITH NOCHECK ADD  CONSTRAINT [FK_Alert_PatientId] FOREIGN KEY([PatientId])
REFERENCES [dbo].[Patient] ([id])
GO
ALTER TABLE [dbo].[Alert] NOCHECK CONSTRAINT [FK_Alert_PatientId]
GO
CREATE INDEX IX_Alert_IsActive_LastActivityAt
    ON dbo.Alert(IsActive, LastActivityAt DESC);
GO
CREATE TABLE dbo.AlertMessage(
    Id int IDENTITY(1,1) PRIMARY KEY NOT NULL,
    AlertId int NOT NULL,
    CreatedAt datetime2(2) NOT NULL DEFAULT SYSDATETIME(),
    Body nvarchar(4000) NOT NULL,
    auth_user_id int NULL,
    FOREIGN KEY (AlertId) REFERENCES dbo.Alert(Id) ON DELETE CASCADE,
    FOREIGN KEY (auth_user_id) REFERENCES dbo.auth_user(id)
);
GO
CREATE INDEX IX_Alert_AlertDefId
    ON dbo.Alert(AlertDefId);
GO
CREATE INDEX IX_AlertMessage_AlertId
    ON dbo.AlertMessage(AlertId);
GO

CREATE TABLE dbo.ProcessState (
    ProcessName varchar(100) PRIMARY KEY NOT NULL,
    LastImportCompletedAt datetime2(2) NOT NULL DEFAULT SYSDATETIME(),
    ProcessingStartedAt datetime2(2) NULL
);
GO

INSERT INTO ProcessState(ProcessName)
VALUES('RuleEngineImport')
GO
CREATE TABLE dbo.Notification(
    Id int IDENTITY(1,1) PRIMARY KEY NOT NULL,
    CreatedAt datetime2(2) NOT NULL DEFAULT SYSDATETIME(),
    ReadAt datetime2(2) NULL,
    IsRead bit NOT NULL DEFAULT 0,
    auth_user_id int NOT NULL,
    AlertMessageId int NOT NULL,
    FOREIGN KEY (auth_user_id) REFERENCES dbo.auth_user(id),
    FOREIGN KEY (AlertMessageId) REFERENCES dbo.AlertMessage(Id) ON DELETE CASCADE,
    UNIQUE (AlertMessageId, auth_user_id)
);
GO
CREATE INDEX IX_Notification_Unread_User_CreatedAt
    ON dbo.Notification(auth_user_id, CreatedAt DESC)
    INCLUDE (AlertMessageId, ReadAt)
    WHERE IsRead = 0;
GO