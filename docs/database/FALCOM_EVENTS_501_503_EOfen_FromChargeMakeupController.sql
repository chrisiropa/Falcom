USE [FG]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF COL_LENGTH(N'dbo.FALCOM_EVENT_OPC_NODES', N'Beschreibung') IS NULL
BEGIN
   ALTER TABLE dbo.FALCOM_EVENT_OPC_NODES
      ADD Beschreibung nvarchar(1024) NULL;
END
GO

MERGE dbo.FALCOM_EVENTS AS target
USING
(
   VALUES
      (CAST(501 AS bigint), N'Event_501', N'EOFEN', N'FALCOM->EOFEN', N'FALCOM sendet Ofenauftrag fuer Furnace[0] an den E-Ofen. OrderID ist der Trigger/Telegrammzaehler.'),
      (CAST(502 AS bigint), N'Event_502', N'EOFEN', N'FALCOM->EOFEN', N'FALCOM sendet Ofenauftrag fuer Furnace[1] an den E-Ofen. OrderID ist der Trigger/Telegrammzaehler.'),
      (CAST(503 AS bigint), N'Event_503', N'EOFEN', N'FALCOM->EOFEN', N'FALCOM sendet Ofenauftrag fuer Furnace[2] an den E-Ofen. OrderID ist der Trigger/Telegrammzaehler.')
) AS source(ID, EventName, Partner, Direction, Description)
ON target.ID = source.ID
WHEN MATCHED THEN
   UPDATE SET
      EventName = source.EventName,
      Partner = source.Partner,
      Direction = source.Direction,
      Description = source.Description,
      IsActive = CAST(1 AS bit)
WHEN NOT MATCHED THEN
   INSERT (ID, EventName, Partner, Direction, Description, IsActive)
   VALUES (source.ID, source.EventName, source.Partner, source.Direction, source.Description, CAST(1 AS bit));
GO

DECLARE @FurnaceIndex int = 0;

WHILE @FurnaceIndex <= 2
BEGIN
   DECLARE @EventID bigint = 501 + @FurnaceIndex;
   DECLARE @BaseNodeID bigint = @EventID * 1000;
   DECLARE @NodePrefix nvarchar(512) =
      N'ns=1;s=EOfen.DataBlocks.FromChargeMakeupController.Furnace.['
      + CONVERT(nvarchar(10), @FurnaceIndex)
      + N'].';

   MERGE dbo.FALCOM_EVENT_OPC_NODES AS target
   USING
   (
      VALUES
         (@BaseNodeID + 1, @EventID, N'FurnaceIndex', @NodePrefix + N'FurnaceIndex', N'Payload', N'Int32', CAST(1 AS bit),
          N'Ofenindex: welcher E-Ofen innerhalb des Furnace-Arrays von FALCOM beauftragt wird.'),
         (@BaseNodeID + 2, @EventID, N'OrderID', @NodePrefix + N'OrderID', N'Trigger', N'Int32', CAST(1 AS bit),
          N'Trigger und Telegrammzaehler von FALCOM Richtung E-Ofen. FALCOM zaehlt diese ID hoch, sobald ein neuer Ofenauftrag anliegt.'),
         (@BaseNodeID + 3, @EventID, N'HeatNumber', @NodePrefix + N'HeatNumber', N'Payload', N'Int32', CAST(1 AS bit),
          N'Fachliche Schmelz- oder Chargennummer, die FALCOM dem E-Ofen fuer diesen Vorgang vorgibt.'),
         (@BaseNodeID + 4, @EventID, N'GradeIdentNumber', @NodePrefix + N'GradeIdentNumber', N'Payload', N'Int32', CAST(1 AS bit),
          N'Numerische E-Ofen-Sortenkennung. Falls FALCOM und E-Ofen unterschiedliche Stammdaten haben, muss diese Kennung gemappt werden.'),
         (@BaseNodeID + 5, @EventID, N'GradeName', @NodePrefix + N'GradeName', N'Payload', N'String', CAST(1 AS bit),
          N'Lesbarer E-Ofen-Sortenname, z.B. GJS400, GJL250 oder GJS500. Muss zur Sortenwelt des E-Ofens passen.'),
         (@BaseNodeID + 6, @EventID, N'WeightSet', @NodePrefix + N'WeightSet', N'Payload', N'Int32', CAST(1 AS bit),
          N'Sollgewicht der Schmelze/Charge in kg, das FALCOM dem E-Ofen fuer diesen Vorgang vorgibt.')
   ) AS source(ID, EventID, NodeName, OPC_Node, NodeRole, DataType, IsRequired, Beschreibung)
   ON target.ID = source.ID
   WHEN MATCHED THEN
      UPDATE SET
         EventID = source.EventID,
         NodeName = source.NodeName,
         OPC_Node = source.OPC_Node,
         NodeRole = source.NodeRole,
         DataType = source.DataType,
         IsRequired = source.IsRequired,
         Beschreibung = source.Beschreibung
   WHEN NOT MATCHED THEN
      INSERT (ID, EventID, NodeName, OPC_Node, NodeRole, DataType, IsRequired, Beschreibung)
      VALUES (source.ID, source.EventID, source.NodeName, source.OPC_Node, source.NodeRole, source.DataType, source.IsRequired, source.Beschreibung);

   SET @FurnaceIndex += 1;
END
GO
