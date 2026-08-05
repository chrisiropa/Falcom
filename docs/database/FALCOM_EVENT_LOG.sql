SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH(N'dbo.FALCOM_EVENT_OPC_NODES', N'LogValue') IS NULL
BEGIN
   ALTER TABLE dbo.FALCOM_EVENT_OPC_NODES
      ADD LogValue bit NOT NULL
         CONSTRAINT DF_FALCOM_EVENT_OPC_NODES_LogValue DEFAULT (0);
END;
GO

IF OBJECT_ID(N'dbo.FALCOM_EVENT_LOG', N'U') IS NULL
BEGIN
   CREATE TABLE dbo.FALCOM_EVENT_LOG
   (
      ID bigint IDENTITY(1,1) NOT NULL
         CONSTRAINT PK_FALCOM_EVENT_LOG PRIMARY KEY,
      Zeitpunkt datetime2(3) NOT NULL
         CONSTRAINT DF_FALCOM_EVENT_LOG_Zeitpunkt DEFAULT (SYSDATETIME()),
      Richtung nvarchar(64) NOT NULL,
      Quelle nvarchar(32) NOT NULL,
      Ziel nvarchar(32) NOT NULL,
      EventName nvarchar(128) NOT NULL,
      EventNummer int NULL,
      PayloadJson nvarchar(max) NULL
   );
END;
GO

IF NOT EXISTS
(
   SELECT 1
   FROM sys.indexes
   WHERE object_id = OBJECT_ID(N'dbo.FALCOM_EVENT_LOG', N'U')
     AND name = N'IX_FALCOM_EVENT_LOG_Zeitpunkt'
)
BEGIN
   CREATE INDEX IX_FALCOM_EVENT_LOG_Zeitpunkt
      ON dbo.FALCOM_EVENT_LOG(Zeitpunkt DESC, ID DESC);
END;
GO

IF NOT EXISTS
(
   SELECT 1
   FROM sys.indexes
   WHERE object_id = OBJECT_ID(N'dbo.FALCOM_EVENT_LOG', N'U')
     AND name = N'IX_FALCOM_EVENT_LOG_EventName_Zeitpunkt'
)
BEGIN
   CREATE INDEX IX_FALCOM_EVENT_LOG_EventName_Zeitpunkt
      ON dbo.FALCOM_EVENT_LOG(EventName, Zeitpunkt DESC, ID DESC);
END;
GO

UPDATE dbo.FALCOM_EVENT_OPC_NODES
SET LogValue = 1
WHERE NodeRole = N'Trigger'
   OR EventID IN (102, 202, 204, 206, 207);
GO

UPDATE dbo.FALCOM_EVENT_OPC_NODES
SET LogValue = 1
WHERE EventID = 104
  AND NodeName IN (N'Event_104');
GO

UPDATE dbo.FALCOM_EVENT_OPC_NODES
SET LogValue = 1
WHERE EventID = 106
  AND NodeName IN (N'Event_106');
GO
