USE [FG]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'dbo.FALCOM_EOFEN_SIM_EventZuordnung', N'U') IS NULL
BEGIN
   CREATE TABLE dbo.FALCOM_EOFEN_SIM_EventZuordnung
   (
      ID bigint IDENTITY(1,1) NOT NULL
         CONSTRAINT PK_FALCOM_EOFEN_SIM_EventZuordnung PRIMARY KEY,
      SourceEventID bigint NULL,
      SourceNodeID bigint NULL,
      TargetEventID bigint NOT NULL,
      TargetNodeID bigint NOT NULL,
      Zuordnungstyp nvarchar(64) NOT NULL,
      Fixwert nvarchar(128) NULL,
      Aktiv bit NOT NULL
         CONSTRAINT DF_FALCOM_EOFEN_SIM_EventZuordnung_Aktiv DEFAULT (1),
      Bemerkung nvarchar(512) NULL,
      AngelegtAm datetime2(3) NOT NULL
         CONSTRAINT DF_FALCOM_EOFEN_SIM_EventZuordnung_AngelegtAm DEFAULT (SYSDATETIME()),
      GeaendertAm datetime2(3) NULL,
      Info nvarchar(256) NULL,
      CONSTRAINT FK_FALCOM_EOFEN_SIM_EventZuordnung_SourceEvent
         FOREIGN KEY (SourceEventID) REFERENCES dbo.FALCOM_EVENTS(ID),
      CONSTRAINT FK_FALCOM_EOFEN_SIM_EventZuordnung_SourceNode
         FOREIGN KEY (SourceNodeID) REFERENCES dbo.FALCOM_EVENT_OPC_NODES(ID),
      CONSTRAINT FK_FALCOM_EOFEN_SIM_EventZuordnung_TargetEvent
         FOREIGN KEY (TargetEventID) REFERENCES dbo.FALCOM_EVENTS(ID),
      CONSTRAINT FK_FALCOM_EOFEN_SIM_EventZuordnung_TargetNode
         FOREIGN KEY (TargetNodeID) REFERENCES dbo.FALCOM_EVENT_OPC_NODES(ID)
   );
END
GO

IF OBJECT_ID(N'dbo.FALCOM_CW_SIM_EventZuordnung', N'U') IS NULL
BEGIN
   CREATE TABLE dbo.FALCOM_CW_SIM_EventZuordnung
   (
      ID bigint IDENTITY(1,1) NOT NULL
         CONSTRAINT PK_FALCOM_CW_SIM_EventZuordnung PRIMARY KEY,
      SourceEventID bigint NULL,
      SourceNodeID bigint NULL,
      TargetEventID bigint NOT NULL,
      TargetNodeID bigint NOT NULL,
      Zuordnungstyp nvarchar(64) NOT NULL,
      Fixwert nvarchar(128) NULL,
      Aktiv bit NOT NULL
         CONSTRAINT DF_FALCOM_CW_SIM_EventZuordnung_Aktiv DEFAULT (1),
      Bemerkung nvarchar(512) NULL,
      AngelegtAm datetime2(3) NOT NULL
         CONSTRAINT DF_FALCOM_CW_SIM_EventZuordnung_AngelegtAm DEFAULT (SYSDATETIME()),
      GeaendertAm datetime2(3) NULL,
      Info nvarchar(256) NULL,
      CONSTRAINT FK_FALCOM_CW_SIM_EventZuordnung_SourceEvent
         FOREIGN KEY (SourceEventID) REFERENCES dbo.FALCOM_EVENTS(ID),
      CONSTRAINT FK_FALCOM_CW_SIM_EventZuordnung_SourceNode
         FOREIGN KEY (SourceNodeID) REFERENCES dbo.FALCOM_EVENT_OPC_NODES(ID),
      CONSTRAINT FK_FALCOM_CW_SIM_EventZuordnung_TargetEvent
         FOREIGN KEY (TargetEventID) REFERENCES dbo.FALCOM_EVENTS(ID),
      CONSTRAINT FK_FALCOM_CW_SIM_EventZuordnung_TargetNode
         FOREIGN KEY (TargetNodeID) REFERENCES dbo.FALCOM_EVENT_OPC_NODES(ID)
   );
END
GO

