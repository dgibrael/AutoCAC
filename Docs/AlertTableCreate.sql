Drop TABLE if exists dbo.ProcessState;
Drop TABLE if exists dbo.RuleEngineFact;
Drop TABLE if exists  dbo.Notification;
Drop TABLE if exists  dbo.AlertMessage;
Drop TABLE if exists  dbo.Alert;
Drop TABLE if exists  dbo.AlertDefRuleNode;
Drop TABLE if exists  dbo.AlertDef;
GO

CREATE TABLE dbo.AlertDef(
    Id int IDENTITY(1,1) PRIMARY KEY NOT NULL,
    Name varchar(50) NOT NULL,
    Origin varchar(50) NOT NULL,
    IsActive bit NOT NULL DEFAULT 1,
    DisplayName VARCHAR(200) NOT NULL,
    Description varchar(1000) NULL,
    UNIQUE (Name)
);
GO
CREATE TABLE dbo.AlertDefRuleNode (
    Id int IDENTITY(1,1) PRIMARY KEY NOT NULL,
    AlertDefId int NOT NULL,
    ParentId int NULL,
    IsGroup bit NOT NULL,
    DataType varchar(50) NULL,
    FieldName varchar(100) NULL,
    Operator varchar(20) NOT NULL,
    Value nvarchar(200) NULL,
    IsActive bit NOT NULL DEFAULT 1,
    CONSTRAINT FK_AlertDefRuleNode_AlertDef FOREIGN KEY (AlertDefId)
        REFERENCES dbo.AlertDef(Id) ON DELETE CASCADE,
    CONSTRAINT FK_AlertDefRuleNode_Parent FOREIGN KEY (ParentId)
        REFERENCES dbo.AlertDefRuleNode(Id)
);
GO
CREATE INDEX IX_AlertDefRuleNode_AlertDefId_ParentId
    ON dbo.AlertDefRuleNode(AlertDefId, ParentId);
GO

CREATE INDEX IX_AlertDefRuleNode_Lookup
    ON dbo.AlertDefRuleNode(DataType, FieldName, AlertDefId)
    WHERE IsGroup = 0;
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
    FOREIGN KEY (AlertDefId) REFERENCES dbo.AlertDef(Id),
    FOREIGN KEY (auth_user_id) REFERENCES dbo.auth_user(id)
);
GO
ALTER TABLE [dbo].[Alert]  WITH NOCHECK ADD  CONSTRAINT [FK_Alert_PatientId] FOREIGN KEY([PatientId])
REFERENCES [dbo].[Patient] ([id])
GO
ALTER TABLE [dbo].[Alert] NOCHECK CONSTRAINT [FK_Alert_PatientId]
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

CREATE TABLE dbo.Notification(
    Id int IDENTITY(1,1) PRIMARY KEY NOT NULL,
    CreatedAt datetime2(2) NOT NULL DEFAULT SYSDATETIME(),
    ReadAt datetime2(2) NULL,
    auth_user_id int NOT NULL,
    AlertMessageId int NOT NULL,
    FOREIGN KEY (auth_user_id) REFERENCES dbo.auth_user(id),
    FOREIGN KEY (AlertMessageId) REFERENCES dbo.AlertMessage(Id) ON DELETE CASCADE,
    UNIQUE (AlertMessageId, auth_user_id)
);
GO

CREATE INDEX IX_Alert_AlertDefId
    ON dbo.Alert(AlertDefId);
GO
CREATE INDEX IX_AlertMessage_AlertId
    ON dbo.AlertMessage(AlertId);
GO
CREATE INDEX IX_Notification_auth_user_id_ReadAt_CreatedAt
    ON dbo.Notification(auth_user_id, ReadAt, CreatedAt DESC);
GO

CREATE TABLE dbo.RuleEngineFact (
    Id bigint IDENTITY(1,1) PRIMARY KEY NOT NULL,
    DataType varchar(50) NOT NULL,         -- LabResult, MedOrder, Microbio, Drug, PharmacyOrderableItem, etc.
    TableName varchar(50) NULL,
    PatientId int NULL,
    RecordKey varchar(100) NOT NULL,       -- actual source/business record identifier
    Instance int NOT NULL DEFAULT 0,
    FieldName varchar(100) NOT NULL,       -- Result, DrugClass, Dose, Susceptibility, etc.
    FieldValue nvarchar(4000) NULL,
    IsActive bit NOT NULL DEFAULT 1,
    LastImportAt datetime2(2) NOT NULL DEFAULT SYSDATETIME(),
    NeedsProcessing bit NOT NULL DEFAULT 1
);
GO
CREATE UNIQUE INDEX UX_RuleEngineFact
    ON dbo.RuleEngineFact(DataType, TableName, PatientId, RecordKey, FieldName, Instance);
GO
CREATE INDEX IX_RuleEngineFact_Pending
    ON dbo.RuleEngineFact(DataType, FieldName, PatientId, RecordKey)
    WHERE NeedsProcessing = 1 AND IsActive = 1;
GO
CREATE TABLE dbo.ProcessState (
    ProcessName varchar(100) PRIMARY KEY NOT NULL,
    LastImportCompletedAt datetime2(2) NOT NULL DEFAULT SYSDATETIME(),
    ProcessingStartedAt datetime2(2) NULL
);
GO