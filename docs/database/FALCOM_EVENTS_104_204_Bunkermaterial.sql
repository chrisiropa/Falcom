SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

MERGE dbo.FALCOM_EVENTS AS target
USING (VALUES
   (CAST(104 AS bigint), N'Event_104', N'FALCOM->KRAN_SPS', N'Antwort von FALCOM mit Bunkernummer und Materialnummer fuer maximal 21 Lagerboxen.', CAST(1 AS bit)),
   (CAST(204 AS bigint), N'Event_204', N'KRAN_SPS->FALCOM', N'Anforderung der aktuellen Bunker- und Materialzuordnung durch die Kran-SPS.', CAST(1 AS bit))
) AS source (ID, EventName, Direction, Description, IsActive)
ON target.ID = source.ID
WHEN MATCHED THEN
   UPDATE SET EventName = source.EventName, Direction = source.Direction,
              Description = source.Description, IsActive = source.IsActive
WHEN NOT MATCHED THEN
   INSERT (ID, EventName, Direction, Description, IsActive)
   VALUES (source.ID, source.EventName, source.Direction, source.Description, source.IsActive);

MERGE dbo.FALCOM_EVENT_OPC_NODES AS target
USING (VALUES
   (CAST(104001 AS bigint), CAST(104 AS bigint), N'Event_104', N'Event_104', N'ns=1;s=Kran.DataBlocks.General_Falcom->Kran.Event_104', N'Trigger', N'Int32', CAST(1 AS bit)),
   (CAST(204001 AS bigint), CAST(204 AS bigint), N'Event_204', N'Event_204', N'ns=1;s=Kran.DataBlocks.General_Kran->Falcom.Event_204', N'Trigger', N'Int32', CAST(1 AS bit))
) AS source (ID, EventID, NodeName, MeissnerNodeName, OPC_Node, NodeRole, DataType, IsRequired)
ON target.ID = source.ID
WHEN MATCHED THEN
   UPDATE SET EventID = source.EventID, NodeName = source.NodeName,
              MeissnerNodeName = source.MeissnerNodeName, OPC_Node = source.OPC_Node,
              NodeRole = source.NodeRole, DataType = source.DataType, IsRequired = source.IsRequired
WHEN NOT MATCHED THEN
   INSERT (ID, EventID, NodeName, MeissnerNodeName, OPC_Node, NodeRole, DataType, IsRequired)
   VALUES (source.ID, source.EventID, source.NodeName, source.MeissnerNodeName,
           source.OPC_Node, source.NodeRole, source.DataType, source.IsRequired);

-- Fuer die Anzahl existiert bewusst kein SPS-Item.
DELETE FROM dbo.FALCOM_EVENT_OPC_NODES
WHERE ID = 104002
   OR (EventID = 104 AND NodeName = N'AnzahlBunker');

DECLARE @Index int = 0;
WHILE @Index < 21
BEGIN
   DECLARE @IndexText char(2) = RIGHT('0' + CONVERT(varchar(2), @Index), 2);
   DECLARE @BaseID bigint = 104003 + (@Index * 2);

   MERGE dbo.FALCOM_EVENT_OPC_NODES AS target
   USING (VALUES
      (@BaseID, CAST(104 AS bigint), N'Bunker' + @IndexText + N'_BuNr', N'Bunker' + @IndexText + N'_BuNr',
       N'ns=1;s=Kran.DataBlocks.104_LNr_MatNr.Bunker.[' + CONVERT(nvarchar(2), @Index) + N'].BuNr', N'Payload', N'Int32', CAST(1 AS bit)),
      (@BaseID + 1, CAST(104 AS bigint), N'Bunker' + @IndexText + N'_MaterialNr', N'Bunker' + @IndexText + N'_MaterialNr',
       N'ns=1;s=Kran.DataBlocks.104_LNr_MatNr.Bunker.[' + CONVERT(nvarchar(2), @Index) + N'].MaterialNr', N'Payload', N'Int32', CAST(1 AS bit))
   ) AS source (ID, EventID, NodeName, MeissnerNodeName, OPC_Node, NodeRole, DataType, IsRequired)
   ON target.ID = source.ID
   WHEN MATCHED THEN
      UPDATE SET EventID = source.EventID, NodeName = source.NodeName,
                 MeissnerNodeName = source.MeissnerNodeName, OPC_Node = source.OPC_Node,
                 NodeRole = source.NodeRole, DataType = source.DataType, IsRequired = source.IsRequired
   WHEN NOT MATCHED THEN
      INSERT (ID, EventID, NodeName, MeissnerNodeName, OPC_Node, NodeRole, DataType, IsRequired)
      VALUES (source.ID, source.EventID, source.NodeName, source.MeissnerNodeName,
              source.OPC_Node, source.NodeRole, source.DataType, source.IsRequired);

   SET @Index += 1;
END;

COMMIT TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_GetBunkerMaterialSnapshot
AS
BEGIN
   SET NOCOUNT ON;

   SELECT
      CONVERT(int, ROW_NUMBER() OVER (ORDER BY lager.Lagerplatz) - 1) AS ArrayIndex,
      CONVERT(int, lager.Lagerplatz) AS BuNr,
      CONVERT(int, COALESCE(material.ID, 0)) AS MaterialNr
   FROM
   (
      SELECT TOP (21) Lagerplatz, MaterialID
      FROM dbo.FALCOM_LAGER
      WHERE PlatzTyp = N'LAGERBOX'
      ORDER BY Lagerplatz
   ) AS lager
   LEFT JOIN dbo.FALCOM_MATERIAL AS material
      ON material.ID = lager.MaterialID
   ORDER BY lager.Lagerplatz;
END;
GO
