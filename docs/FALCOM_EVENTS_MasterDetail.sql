USE [FG];
GO

SET XACT_ABORT ON;
GO

BEGIN TRY
   BEGIN TRANSACTION;

   IF COL_LENGTH(N'dbo.FALCOM_EVENTS', N'OPC_Node') IS NOT NULL
   BEGIN
      EXEC sys.sp_rename
         @objname = N'dbo.FALCOM_EVENTS',
         @newname = N'FALCOM_EVENTS_LEGACY';

      DECLARE @LegacyPrimaryKey sysname =
      (
         SELECT keyConstraints.name
         FROM sys.key_constraints AS keyConstraints
         WHERE keyConstraints.parent_object_id = OBJECT_ID(N'dbo.FALCOM_EVENTS_LEGACY')
           AND keyConstraints.type = N'PK'
      );

      IF @LegacyPrimaryKey IS NOT NULL
      BEGIN
         DECLARE @DropLegacyPrimaryKeySql nvarchar(max) =
            N'ALTER TABLE dbo.FALCOM_EVENTS_LEGACY DROP CONSTRAINT '
            + QUOTENAME(@LegacyPrimaryKey);

         EXEC sys.sp_executesql @DropLegacyPrimaryKeySql;
      END;

      CREATE TABLE dbo.FALCOM_EVENTS
      (
         ID          bigint IDENTITY(1, 1) NOT NULL,
         EventName   nvarchar(128) NOT NULL,
         Direction   nvarchar(128) NOT NULL,
         Description nvarchar(512) NULL,
         IsActive    bit NOT NULL
            CONSTRAINT DF_FALCOM_EVENTS_IsActive DEFAULT (1),
         CONSTRAINT PK_FALCOM_EVENTS PRIMARY KEY CLUSTERED (ID),
         CONSTRAINT UQ_FALCOM_EVENTS_EventName UNIQUE (EventName),
         CONSTRAINT CK_FALCOM_EVENTS_Direction CHECK (
            Direction IN (
               N'FALCOM->KRAN_SPS',
               N'KRAN_SPS->FALCOM'))
      );

      CREATE TABLE dbo.FALCOM_EVENT_OPC_NODES
      (
         ID         bigint IDENTITY(1, 1) NOT NULL,
         EventID    bigint NOT NULL,
         NodeName   nvarchar(128) NOT NULL,
         OPC_Node   nvarchar(1024) NOT NULL,
         NodeRole   nvarchar(16) NOT NULL,
         DataType   nvarchar(64) NULL,
         IsRequired bit NOT NULL
            CONSTRAINT DF_FALCOM_EVENT_OPC_NODES_IsRequired DEFAULT (1),
         CONSTRAINT PK_FALCOM_EVENT_OPC_NODES PRIMARY KEY CLUSTERED (ID),
         CONSTRAINT FK_FALCOM_EVENT_OPC_NODES_EVENTS
            FOREIGN KEY (EventID) REFERENCES dbo.FALCOM_EVENTS(ID)
            ON DELETE CASCADE,
         CONSTRAINT UQ_FALCOM_EVENT_OPC_NODES_EventID_NodeName
            UNIQUE (EventID, NodeName),
         CONSTRAINT CK_FALCOM_EVENT_OPC_NODES_NodeRole
            CHECK (NodeRole IN (N'Trigger', N'Payload'))
      );

      INSERT INTO dbo.FALCOM_EVENTS (EventName, Direction, Description)
      SELECT DISTINCT
         CASE
            WHEN CHARINDEX(N'.', legacy.EventName) > 0
               THEN LEFT(legacy.EventName, CHARINDEX(N'.', legacy.EventName) - 1)
            ELSE legacy.EventName
         END,
         N'KRAN_SPS->FALCOM',
         NULL
      FROM dbo.FALCOM_EVENTS_LEGACY AS legacy;

      INSERT INTO dbo.FALCOM_EVENT_OPC_NODES
         (EventID, NodeName, OPC_Node, NodeRole, DataType, IsRequired)
      SELECT
         events.ID,
         CASE
            WHEN CHARINDEX(N'.', legacy.EventName) > 0
               THEN SUBSTRING(
                  legacy.EventName,
                  CHARINDEX(N'.', legacy.EventName) + 1,
                  LEN(legacy.EventName))
            ELSE N'Default'
         END,
         legacy.OPC_Node,
         CASE
            WHEN legacy.EventName LIKE N'%.AenderungsZaehler' THEN N'Trigger'
            ELSE N'Payload'
         END,
         CASE
            WHEN legacy.EventName LIKE N'%.AenderungsZaehler' THEN N'Boolean'
            WHEN legacy.EventName LIKE N'%.AuftragsNummer'
              OR legacy.EventName LIKE N'%.TeilfahrtID'
              OR legacy.EventName LIKE N'%.Fehlercode' THEN N'Int32'
            WHEN legacy.EventName LIKE N'%.Toleranz'
              OR legacy.EventName LIKE N'%.IstGewicht' THEN N'Double'
            WHEN legacy.EventName LIKE N'%.KranQuelle'
              OR legacy.EventName LIKE N'%.KranZiel' THEN N'String'
            ELSE NULL
         END,
         1
      FROM dbo.FALCOM_EVENTS_LEGACY AS legacy
      INNER JOIN dbo.FALCOM_EVENTS AS events
         ON events.EventName =
            CASE
               WHEN CHARINDEX(N'.', legacy.EventName) > 0
                  THEN LEFT(legacy.EventName, CHARINDEX(N'.', legacy.EventName) - 1)
               ELSE legacy.EventName
            END;

      DROP TABLE dbo.FALCOM_EVENTS_LEGACY;
   END;

   IF OBJECT_ID(N'dbo.FALCOM_EVENTS', N'U') IS NULL
   BEGIN
      CREATE TABLE dbo.FALCOM_EVENTS
      (
         ID          bigint IDENTITY(1, 1) NOT NULL,
         EventName   nvarchar(128) NOT NULL,
         Direction   nvarchar(128) NOT NULL,
         Description nvarchar(512) NULL,
         IsActive    bit NOT NULL
            CONSTRAINT DF_FALCOM_EVENTS_IsActive DEFAULT (1),
         CONSTRAINT PK_FALCOM_EVENTS PRIMARY KEY CLUSTERED (ID),
         CONSTRAINT UQ_FALCOM_EVENTS_EventName UNIQUE (EventName),
         CONSTRAINT CK_FALCOM_EVENTS_Direction CHECK (
            Direction IN (
               N'FALCOM->KRAN_SPS',
               N'KRAN_SPS->FALCOM'))
      );
   END;

   IF OBJECT_ID(N'dbo.FALCOM_EVENT_OPC_NODES', N'U') IS NULL
   BEGIN
      CREATE TABLE dbo.FALCOM_EVENT_OPC_NODES
      (
         ID         bigint IDENTITY(1, 1) NOT NULL,
         EventID    bigint NOT NULL,
         NodeName   nvarchar(128) NOT NULL,
         OPC_Node   nvarchar(1024) NOT NULL,
         NodeRole   nvarchar(16) NOT NULL,
         DataType   nvarchar(64) NULL,
         IsRequired bit NOT NULL
            CONSTRAINT DF_FALCOM_EVENT_OPC_NODES_IsRequired DEFAULT (1),
         CONSTRAINT PK_FALCOM_EVENT_OPC_NODES PRIMARY KEY CLUSTERED (ID),
         CONSTRAINT FK_FALCOM_EVENT_OPC_NODES_EVENTS
            FOREIGN KEY (EventID) REFERENCES dbo.FALCOM_EVENTS(ID)
            ON DELETE CASCADE,
         CONSTRAINT UQ_FALCOM_EVENT_OPC_NODES_EventID_NodeName
            UNIQUE (EventID, NodeName),
         CONSTRAINT CK_FALCOM_EVENT_OPC_NODES_NodeRole
            CHECK (NodeRole IN (N'Trigger', N'Payload'))
      );
   END;

   EXEC sys.sp_executesql N'
      MERGE dbo.FALCOM_EVENTS AS target
      USING (VALUES
         (N''KranfahrtBeendet'', N''KRAN_SPS->FALCOM'', N''Signalisiert das Ende einer physischen Kranfahrt.'', CAST(1 AS bit))
      ) AS source (EventName, Direction, Description, IsActive)
      ON target.EventName = source.EventName
      WHEN MATCHED THEN
         UPDATE SET
            target.Direction = source.Direction,
            target.Description = source.Description,
            target.IsActive = source.IsActive
      WHEN NOT MATCHED THEN
         INSERT (EventName, Direction, Description, IsActive)
         VALUES (source.EventName, source.Direction, source.Description, source.IsActive);';

   DECLARE @KranfahrtBeendetEventID bigint =
      (SELECT ID FROM dbo.FALCOM_EVENTS WHERE EventName = N'KranfahrtBeendet');

   MERGE dbo.FALCOM_EVENT_OPC_NODES AS target
   USING (VALUES
      (N'AenderungsZaehler', N'ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Comands.Stop',    N'Trigger', N'Boolean'),
      (N'AuftragsNummer',    N'ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.AuftragID',       N'Payload', N'Int32'),
      (N'TeilfahrtID',       N'ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.TeilfahrtNodeId', N'Payload', N'Int32'),
      (N'KranQuelle',        N'ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.KranQuelle',       N'Payload', N'String'),
      (N'KranZiel',          N'ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.KranZiel',         N'Payload', N'String'),
      (N'Toleranz',          N'ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Toleranz',         N'Payload', N'Double'),
      (N'IstGewicht',        N'ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.IstGewicht',       N'Payload', N'Double'),
      (N'Fehlercode',        N'ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Fehlercode',       N'Payload', N'Int32')
   ) AS source (NodeName, OPC_Node, NodeRole, DataType)
   ON target.EventID = @KranfahrtBeendetEventID
      AND target.NodeName = source.NodeName
   WHEN MATCHED THEN
      UPDATE SET
         target.OPC_Node = source.OPC_Node,
         target.NodeRole = source.NodeRole,
         target.DataType = source.DataType,
         target.IsRequired = 1
   WHEN NOT MATCHED THEN
      INSERT (EventID, NodeName, OPC_Node, NodeRole, DataType, IsRequired)
      VALUES (
         @KranfahrtBeendetEventID,
         source.NodeName,
         source.OPC_Node,
         source.NodeRole,
         source.DataType,
         1
      );

   COMMIT TRANSACTION;
END TRY
BEGIN CATCH
   IF @@TRANCOUNT > 0
      ROLLBACK TRANSACTION;

   THROW;
END CATCH;
GO
