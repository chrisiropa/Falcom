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

DELETE FROM dbo.FALCOM_EVENT_OPC_NODES
 WHERE EventID BETWEEN 501 AND 503;
GO

DELETE FROM dbo.FALCOM_EVENTS
 WHERE ID BETWEEN 501 AND 503;
GO

MERGE dbo.FALCOM_EVENTS AS target
USING
(
   VALUES
      (CAST(601 AS bigint), N'Event_601', N'EOFEN', N'EOFEN->FALCOM', N'E-Ofen Furnace[0] meldet Chargen-/Schmelzstatus an FALCOM. OrderID ist der Trigger/Telegrammzaehler.'),
      (CAST(602 AS bigint), N'Event_602', N'EOFEN', N'EOFEN->FALCOM', N'E-Ofen Furnace[1] meldet Chargen-/Schmelzstatus an FALCOM. OrderID ist der Trigger/Telegrammzaehler.'),
      (CAST(603 AS bigint), N'Event_603', N'EOFEN', N'EOFEN->FALCOM', N'E-Ofen Furnace[2] meldet Chargen-/Schmelzstatus an FALCOM. OrderID ist der Trigger/Telegrammzaehler.')
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
   DECLARE @EventID bigint = 601 + @FurnaceIndex;
   DECLARE @BaseNodeID bigint = @EventID * 1000;
   DECLARE @NodePrefix nvarchar(512) =
      N'ns=1;s=EOfen.DataBlocks.ToChargeMakeupController.Furnace.['
      + CONVERT(nvarchar(10), @FurnaceIndex)
      + N'].';

   MERGE dbo.FALCOM_EVENT_OPC_NODES AS target
   USING
   (
      VALUES
         (@BaseNodeID + 1, @EventID, N'FurnaceIndex', @NodePrefix + N'FurnaceIndex', N'Payload', N'Int32', CAST(1 AS bit),
          N'Ofenindex: welcher E-Ofen innerhalb des Furnace-Arrays gemeldet wird.'),
         (@BaseNodeID + 2, @EventID, N'OrderID', @NodePrefix + N'OrderID', N'Trigger', N'Int32', CAST(1 AS bit),
          N'Trigger und Telegrammzaehler vom E-Ofen Richtung FALCOM. Der E-Ofen zaehlt diese ID hoch, sobald neue Informationen fuer FALCOM anliegen.'),
         (@BaseNodeID + 3, @EventID, N'HeatNumber', @NodePrefix + N'HeatNumber', N'Payload', N'Int32', CAST(1 AS bit),
          N'Fachliche Schmelz- oder Chargennummer des E-Ofens.'),
         (@BaseNodeID + 4, @EventID, N'GradeIdentNumber', @NodePrefix + N'GradeIdentNumber', N'Payload', N'Int32', CAST(1 AS bit),
          N'Numerische E-Ofen-Sortenkennung zur gemeldeten Schmelze/Charge.'),
         (@BaseNodeID + 5, @EventID, N'GradeName', @NodePrefix + N'GradeName', N'Payload', N'String', CAST(1 AS bit),
          N'Lesbarer E-Ofen-Sortenname, z.B. GJS400, GJL250 oder GJS500. Muss zur Sortenwelt des E-Ofens passen.'),
         (@BaseNodeID + 6, @EventID, N'WeightSet', @NodePrefix + N'WeightSet', N'Payload', N'Int32', CAST(1 AS bit),
          N'Sollgewicht der Schmelze/Charge in kg, wie es dem E-Ofen fuer diesen Vorgang bekannt ist.'),
         (@BaseNodeID + 7, @EventID, N'TargetTemperature', @NodePrefix + N'TargetTemperature', N'Payload', N'Int32', CAST(1 AS bit),
          N'Zieltemperatur der Schmelze/Charge.'),
         (@BaseNodeID + 8, @EventID, N'ActualTemperature', @NodePrefix + N'ActualTemperature', N'Payload', N'Int32', CAST(1 AS bit),
          N'Aktuelle Ist-Temperatur des E-Ofens zur gemeldeten Schmelze/Charge.'),
         (@BaseNodeID + 9, @EventID, N'TimeHeatStart', @NodePrefix + N'TimeHeatStart', N'Payload', N'DateTime', CAST(1 AS bit),
          N'Zeitpunkt, zu dem die Schmelze/Charge im E-Ofen gestartet wurde.'),
         (@BaseNodeID + 10, @EventID, N'TimeTargetTemperatureReached', @NodePrefix + N'TimeTargetTemperatureReached', N'Payload', N'DateTime', CAST(1 AS bit),
          N'Zeitpunkt, zu dem die Zieltemperatur erreicht wurde.'),
         (@BaseNodeID + 11, @EventID, N'EnergyUsage', @NodePrefix + N'EnergyUsage', N'Payload', N'Int32', CAST(1 AS bit),
          N'Energieverbrauch des E-Ofens fuer die gemeldete Schmelze/Charge.')
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
