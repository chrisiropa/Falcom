SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

MERGE dbo.FALCOM_EVENTS AS target
USING (VALUES
   (CAST(106 AS bigint), N'Event_106', N'FALCOM->KRAN_SPS', N'Antwort von FALCOM mit Kranpositionsdaten aus FALCOM_KRAN_POSITION.', CAST(1 AS bit)),
   (CAST(206 AS bigint), N'Event_206', N'KRAN_SPS->FALCOM', N'Anforderung der aktuellen Kranpositionsdaten durch die Kran-SPS.', CAST(1 AS bit))
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
   (CAST(106001 AS bigint), CAST(106 AS bigint), N'Event_106', N'ns=1;s=Kran.DataBlocks.General_Falcom->Kran.Event_106', N'Trigger', N'Int32', CAST(1 AS bit)),
   (CAST(206001 AS bigint), CAST(206 AS bigint), N'Event_206', N'ns=1;s=Kran.DataBlocks.General_Kran->Falcom.Event_206', N'Trigger', N'Int32', CAST(1 AS bit))
) AS source (ID, EventID, NodeName, OPC_Node, NodeRole, DataType, IsRequired)
ON target.ID = source.ID
WHEN MATCHED THEN
   UPDATE SET EventID = source.EventID, NodeName = source.NodeName, OPC_Node = source.OPC_Node,
              NodeRole = source.NodeRole, DataType = source.DataType, IsRequired = source.IsRequired
WHEN NOT MATCHED THEN
   INSERT (ID, EventID, NodeName, OPC_Node, NodeRole, DataType, IsRequired)
   VALUES (source.ID, source.EventID, source.NodeName,
           source.OPC_Node, source.NodeRole, source.DataType, source.IsRequired);

DELETE FROM dbo.FALCOM_EVENT_OPC_NODES
WHERE EventID = 106
  AND NodeRole = N'Payload';

DECLARE @ObjectIndex int = 0;
WHILE @ObjectIndex <= 50
BEGIN
   DECLARE @ObjectIndexText char(3) = RIGHT('00' + CONVERT(varchar(3), @ObjectIndex), 3);
   DECLARE @ObjectNodePrefix nvarchar(256) = N'ns=1;s=Kran.DataBlocks.106_Positionen.Objekt.[' + CONVERT(nvarchar(10), @ObjectIndex) + N'].';
   DECLARE @ObjectBaseID bigint = CAST(106100 AS bigint) + (@ObjectIndex * 100);

   INSERT INTO dbo.FALCOM_EVENT_OPC_NODES
      (ID, EventID, NodeName, OPC_Node, NodeRole, DataType, IsRequired)
   VALUES
      (@ObjectBaseID + 0, CAST(106 AS bigint), N'Objekt' + @ObjectIndexText + N'_stArt', @ObjectNodePrefix + N'stArt', N'Payload', N'String', CAST(1 AS bit)),
      (@ObjectBaseID + 1, CAST(106 AS bigint), N'Objekt' + @ObjectIndexText + N'_stBezeichnung', @ObjectNodePrefix + N'stBezeichnung', N'Payload', N'String', CAST(1 AS bit)),
      (@ObjectBaseID + 2, CAST(106 AS bigint), N'Objekt' + @ObjectIndexText + N'_stPositionsTyp', @ObjectNodePrefix + N'stPositionsTyp', N'Payload', N'String', CAST(1 AS bit)),
      (@ObjectBaseID + 3, CAST(106 AS bigint), N'Objekt' + @ObjectIndexText + N'_iID', @ObjectNodePrefix + N'iID', N'Payload', N'Int32', CAST(1 AS bit)),
      (@ObjectBaseID + 4, CAST(106 AS bigint), N'Objekt' + @ObjectIndexText + N'_diKatze_Start_X', @ObjectNodePrefix + N'diKatze_Start_X', N'Payload', N'Int32', CAST(1 AS bit)),
      (@ObjectBaseID + 5, CAST(106 AS bigint), N'Objekt' + @ObjectIndexText + N'_diKatze_Breite_X', @ObjectNodePrefix + N'diKatze_Breite_X', N'Payload', N'Int32', CAST(1 AS bit)),
      (@ObjectBaseID + 6, CAST(106 AS bigint), N'Objekt' + @ObjectIndexText + N'_diKran_Start_Y', @ObjectNodePrefix + N'diKran_Start_Y', N'Payload', N'Int32', CAST(1 AS bit)),
      (@ObjectBaseID + 7, CAST(106 AS bigint), N'Objekt' + @ObjectIndexText + N'_diKran_Laenge_Y', @ObjectNodePrefix + N'diKran_Laenge_Y', N'Payload', N'Int32', CAST(1 AS bit)),
      (@ObjectBaseID + 8, CAST(106 AS bigint), N'Objekt' + @ObjectIndexText + N'_diHub_Start_Z', @ObjectNodePrefix + N'diHub_Start_Z', N'Payload', N'Int32', CAST(1 AS bit)),
      (@ObjectBaseID + 9, CAST(106 AS bigint), N'Objekt' + @ObjectIndexText + N'_diHub_Hoehe_Z', @ObjectNodePrefix + N'diHub_Hoehe_Z', N'Payload', N'Int32', CAST(1 AS bit)),
      (@ObjectBaseID + 10, CAST(106 AS bigint), N'Objekt' + @ObjectIndexText + N'_iPositionsAnz', @ObjectNodePrefix + N'iPositionsAnz', N'Payload', N'Int32', CAST(1 AS bit));

   DECLARE @PositionIndex int = 0;
   WHILE @PositionIndex <= 9
   BEGIN
      DECLARE @PositionIndexText char(3) = RIGHT('00' + CONVERT(varchar(3), @PositionIndex), 3);
      DECLARE @PositionNodePrefix nvarchar(320) = @ObjectNodePrefix + N'Positions.[' + CONVERT(nvarchar(10), @PositionIndex) + N'].Pos.';
      DECLARE @PositionBaseID bigint = @ObjectBaseID + 20 + (@PositionIndex * 2);

      INSERT INTO dbo.FALCOM_EVENT_OPC_NODES
         (ID, EventID, NodeName, OPC_Node, NodeRole, DataType, IsRequired)
      VALUES
         (@PositionBaseID, CAST(106 AS bigint), N'Objekt' + @ObjectIndexText + N'_Positions' + @PositionIndexText + N'_diKatze_X', @PositionNodePrefix + N'diKatze_X', N'Payload', N'Int32', CAST(1 AS bit)),
         (@PositionBaseID + 1, CAST(106 AS bigint), N'Objekt' + @ObjectIndexText + N'_Positions' + @PositionIndexText + N'_diKran_Y', @PositionNodePrefix + N'diKran_Y', N'Payload', N'Int32', CAST(1 AS bit));

      SET @PositionIndex += 1;
   END;

   SET @ObjectIndex += 1;
END;

COMMIT TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_GetKranPositionenSnapshot
AS
BEGIN
   SET NOCOUNT ON;

   ;WITH
   Objekt AS
   (
      SELECT
         ArrayIndex = CONVERT(int, p.ID),
         iID = CONVERT(int, p.ID),
         stArt = CONVERT(nvarchar(64), COALESCE(p.Art, N'')),
         stBezeichnung = CONVERT(nvarchar(128), COALESCE(p.Bezeichnung, N'')),
         stPositionsTyp = CONVERT(nvarchar(64), COALESCE(p.PositionsTyp, N'')),
         diKatze_Start_X = CONVERT(int, COALESCE(p.Start_KatzeX, 0)),
         diKatze_Breite_X = CONVERT(int, COALESCE(p.Breite, 0)),
         diKran_Start_Y = CONVERT(int, COALESCE(p.Start_KranY, 0)),
         diKran_Laenge_Y = CONVERT(int, COALESCE(p.Laenge, 0)),
         diHub_Start_Z = CONVERT(int, COALESCE(p.StartHubwerkZ, 0)),
         diHub_Hoehe_Z = CONVERT(int, COALESCE(p.HoeheHubwerkZ, 0))
      FROM dbo.FALCOM_KRAN_POSITION AS p
      WHERE p.ID BETWEEN 0 AND 50
   ),
   AnfahrpunkteRanked AS
   (
      SELECT
         a.PositionID,
         PositionArrayIndex = CONVERT(int, ROW_NUMBER() OVER (PARTITION BY a.PositionID ORDER BY a.PunktNr) - 1),
         diKatze_X = CONVERT(int, a.KatzeX),
         diKran_Y = CONVERT(int, a.KranY)
      FROM dbo.FALCOM_GetKranPositionAnfahrpunkte() AS a
   ),
   Anfahrpunkte AS
   (
      SELECT
         PositionID,
         PositionArrayIndex,
         diKatze_X,
         diKran_Y
      FROM AnfahrpunkteRanked
      WHERE PositionArrayIndex BETWEEN 0 AND 9
   ),
   AnfahrpunktAnzahl AS
   (
      SELECT
         PositionID,
         iPositionsAnz = CONVERT(int, COUNT_BIG(*))
      FROM Anfahrpunkte
      GROUP BY PositionID
   )
   SELECT
      o.ArrayIndex,
      o.stArt,
      o.stBezeichnung,
      o.stPositionsTyp,
      o.iID,
      o.diKatze_Start_X,
      o.diKatze_Breite_X,
      o.diKran_Start_Y,
      o.diKran_Laenge_Y,
      o.diHub_Start_Z,
      o.diHub_Hoehe_Z,
      iPositionsAnz = CONVERT(int, COALESCE(aa.iPositionsAnz, 0)),
      a.PositionArrayIndex,
      a.diKatze_X,
      a.diKran_Y
   FROM Objekt AS o
   LEFT JOIN AnfahrpunktAnzahl AS aa
      ON aa.PositionID = o.iID
   LEFT JOIN Anfahrpunkte AS a
      ON a.PositionID = o.iID
   ORDER BY o.ArrayIndex, a.PositionArrayIndex;
END;
GO
