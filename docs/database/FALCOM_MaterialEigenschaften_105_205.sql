SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'Datum_Zeit') IS NULL
   ALTER TABLE dbo.FALCOM_MATERIAL ADD Datum_Zeit datetime2(0) NULL;

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'diMasseGattMin') IS NULL
   ALTER TABLE dbo.FALCOM_MATERIAL ADD diMasseGattMin int NOT NULL CONSTRAINT DF_FALCOM_MATERIAL_diMasseGattMin DEFAULT (0);

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'diZeitAbtippen') IS NULL
   ALTER TABLE dbo.FALCOM_MATERIAL ADD diZeitAbtippen int NOT NULL CONSTRAINT DF_FALCOM_MATERIAL_diZeitAbtippen DEFAULT (0);

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'diMasseVorAbtippen') IS NULL
   ALTER TABLE dbo.FALCOM_MATERIAL ADD diMasseVorAbtippen int NOT NULL CONSTRAINT DF_FALCOM_MATERIAL_diMasseVorAbtippen DEFAULT (0);

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'diMasseTol_pos') IS NULL
   ALTER TABLE dbo.FALCOM_MATERIAL ADD diMasseTol_pos int NOT NULL CONSTRAINT DF_FALCOM_MATERIAL_diMasseTol_pos DEFAULT (0);

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'diMasseTol_neg') IS NULL
   ALTER TABLE dbo.FALCOM_MATERIAL ADD diMasseTol_neg int NOT NULL CONSTRAINT DF_FALCOM_MATERIAL_diMasseTol_neg DEFAULT (0);

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'rKraftStufenPro100Kg') IS NULL
   ALTER TABLE dbo.FALCOM_MATERIAL ADD rKraftStufenPro100Kg decimal(18, 3) NOT NULL CONSTRAINT DF_FALCOM_MATERIAL_rKraftStufenPro100Kg DEFAULT (0);

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'xAbwurfFlach') IS NULL
   ALTER TABLE dbo.FALCOM_MATERIAL ADD xAbwurfFlach bit NOT NULL CONSTRAINT DF_FALCOM_MATERIAL_xAbwurfFlach DEFAULT (0);

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'xAbwurfAbzett') IS NULL
   ALTER TABLE dbo.FALCOM_MATERIAL ADD xAbwurfAbzett bit NOT NULL CONSTRAINT DF_FALCOM_MATERIAL_xAbwurfAbzett DEFAULT (0);

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'xAbwurfTippen') IS NULL
   ALTER TABLE dbo.FALCOM_MATERIAL ADD xAbwurfTippen bit NOT NULL CONSTRAINT DF_FALCOM_MATERIAL_xAbwurfTippen DEFAULT (0);

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'xNachfassenMag_Aus') IS NULL
   ALTER TABLE dbo.FALCOM_MATERIAL ADD xNachfassenMag_Aus bit NOT NULL CONSTRAINT DF_FALCOM_MATERIAL_xNachfassenMag_Aus DEFAULT (0);

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'xNachfassenMag_Dauernd') IS NULL
   ALTER TABLE dbo.FALCOM_MATERIAL ADD xNachfassenMag_Dauernd bit NOT NULL CONSTRAINT DF_FALCOM_MATERIAL_xNachfassenMag_Dauernd DEFAULT (0);

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'xKreislauf') IS NULL
   ALTER TABLE dbo.FALCOM_MATERIAL ADD xKreislauf bit NOT NULL CONSTRAINT DF_FALCOM_MATERIAL_xKreislauf DEFAULT (0);

MERGE dbo.FALCOM_EVENTS AS target
USING (VALUES
   (CAST(105 AS bigint), N'Event_105', N'FALCOM->KRAN_SPS', N'Antwort von FALCOM mit Material-Eigenschaften.', CAST(1 AS bit)),
   (CAST(205 AS bigint), N'Event_205', N'KRAN_SPS->FALCOM', N'Anforderung der aktuellen Material-Eigenschaften durch die Kran-SPS.', CAST(1 AS bit))
) AS source (ID, EventName, Direction, Description, IsActive)
ON target.ID = source.ID
WHEN MATCHED THEN
   UPDATE SET EventName = source.EventName,
              Direction = source.Direction,
              Description = source.Description,
              IsActive = source.IsActive
WHEN NOT MATCHED THEN
   INSERT (ID, EventName, Direction, Description, IsActive)
   VALUES (source.ID, source.EventName, source.Direction, source.Description, source.IsActive);

MERGE dbo.FALCOM_EVENT_OPC_NODES AS target
USING (VALUES
   (CAST(105001 AS bigint), CAST(105 AS bigint), N'Event_105', N'ns=1;s=Kran.DataBlocks.General_Falcom->Kran.Event_105', N'Trigger', N'Int32', CAST(1 AS bit)),
   (CAST(205001 AS bigint), CAST(205 AS bigint), N'Event_205', N'ns=1;s=Kran.DataBlocks.General_Kran->Falcom.Event_205', N'Trigger', N'Int32', CAST(1 AS bit))
) AS source (ID, EventID, NodeName, OPC_Node, NodeRole, DataType, IsRequired)
ON target.ID = source.ID
WHEN MATCHED THEN
   UPDATE SET EventID = source.EventID,
              NodeName = source.NodeName,
              OPC_Node = source.OPC_Node,
              NodeRole = source.NodeRole,
              DataType = source.DataType,
              IsRequired = source.IsRequired
WHEN NOT MATCHED THEN
   INSERT (ID, EventID, NodeName, OPC_Node, NodeRole, DataType, IsRequired)
   VALUES (source.ID, source.EventID, source.NodeName,
           source.OPC_Node, source.NodeRole, source.DataType, source.IsRequired);

DELETE FROM dbo.FALCOM_EVENT_OPC_NODES
WHERE EventID = 105
  AND NodeRole = N'Payload';

DECLARE @MaterialIndex int = 0;
WHILE @MaterialIndex <= 20
BEGIN
   DECLARE @MaterialIndexText char(3) = RIGHT('00' + CONVERT(varchar(3), @MaterialIndex), 3);
   DECLARE @MaterialNodePrefix nvarchar(256) = N'ns=1;s=Kran.DataBlocks.105_MatEigen.Material.[' + CONVERT(nvarchar(10), @MaterialIndex) + N'].';
   DECLARE @MaterialBaseID bigint = CAST(1050000 AS bigint) + (@MaterialIndex * 100);

   INSERT INTO dbo.FALCOM_EVENT_OPC_NODES
      (ID, EventID, NodeName, OPC_Node, NodeRole, DataType, IsRequired)
   VALUES
      (@MaterialBaseID + 0, CAST(105 AS bigint), N'Material' + @MaterialIndexText + N'_iID', @MaterialNodePrefix + N'iID', N'Payload', N'Int32', CAST(1 AS bit)),
      (@MaterialBaseID + 1, CAST(105 AS bigint), N'Material' + @MaterialIndexText + N'_Mat_Name', @MaterialNodePrefix + N'Mat_Name', N'Payload', N'String', CAST(1 AS bit)),
      (@MaterialBaseID + 2, CAST(105 AS bigint), N'Material' + @MaterialIndexText + N'_Datum_Zeit', @MaterialNodePrefix + N'Datum_Zeit', N'Payload', N'DateTime', CAST(1 AS bit)),
      (@MaterialBaseID + 10, CAST(105 AS bigint), N'Material' + @MaterialIndexText + N'_PARA_diMasseGattMin', @MaterialNodePrefix + N'PARA.diMasseGattMin', N'Payload', N'Int32', CAST(1 AS bit)),
      (@MaterialBaseID + 11, CAST(105 AS bigint), N'Material' + @MaterialIndexText + N'_PARA_diZeitAbtippen', @MaterialNodePrefix + N'PARA.diZeitAbtippen', N'Payload', N'Int32', CAST(1 AS bit)),
      (@MaterialBaseID + 12, CAST(105 AS bigint), N'Material' + @MaterialIndexText + N'_PARA_diMasseVorAbtippen', @MaterialNodePrefix + N'PARA.diMasseVorAbtippen', N'Payload', N'Int32', CAST(1 AS bit)),
      (@MaterialBaseID + 13, CAST(105 AS bigint), N'Material' + @MaterialIndexText + N'_PARA_diMasseTol_pos', @MaterialNodePrefix + N'PARA.diMasseTol_pos', N'Payload', N'Int32', CAST(1 AS bit)),
      (@MaterialBaseID + 14, CAST(105 AS bigint), N'Material' + @MaterialIndexText + N'_PARA_diMasseTol_neg', @MaterialNodePrefix + N'PARA.diMasseTol_neg', N'Payload', N'Int32', CAST(1 AS bit)),
      (@MaterialBaseID + 15, CAST(105 AS bigint), N'Material' + @MaterialIndexText + N'_PARA_rKraftStufenPro100Kg', @MaterialNodePrefix + N'PARA.rKraftStufenPro100Kg', N'Payload', N'Float', CAST(1 AS bit)),
      (@MaterialBaseID + 16, CAST(105 AS bigint), N'Material' + @MaterialIndexText + N'_PARA_xAbwurfFlach', @MaterialNodePrefix + N'PARA.xAbwurfFlach', N'Payload', N'Bit', CAST(1 AS bit)),
      (@MaterialBaseID + 17, CAST(105 AS bigint), N'Material' + @MaterialIndexText + N'_PARA_xAbwurfAbzett', @MaterialNodePrefix + N'PARA.xAbwurfAbzett', N'Payload', N'Bit', CAST(1 AS bit)),
      (@MaterialBaseID + 18, CAST(105 AS bigint), N'Material' + @MaterialIndexText + N'_PARA_xAbwurfTippen', @MaterialNodePrefix + N'PARA.xAbwurfTippen', N'Payload', N'Bit', CAST(1 AS bit)),
      (@MaterialBaseID + 19, CAST(105 AS bigint), N'Material' + @MaterialIndexText + N'_PARA_xNachfassenMag_Aus', @MaterialNodePrefix + N'PARA.xNachfassenMag_Aus', N'Payload', N'Bit', CAST(1 AS bit)),
      (@MaterialBaseID + 20, CAST(105 AS bigint), N'Material' + @MaterialIndexText + N'_PARA_xNachfassenMag_Dauernd', @MaterialNodePrefix + N'PARA.xNachfassenMag_Dauernd', N'Payload', N'Bit', CAST(1 AS bit)),
      (@MaterialBaseID + 21, CAST(105 AS bigint), N'Material' + @MaterialIndexText + N'_PARA_xKreislauf', @MaterialNodePrefix + N'PARA.xKreislauf', N'Payload', N'Bit', CAST(1 AS bit));

   SET @MaterialIndex += 1;
END;

COMMIT TRANSACTION;
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_GetMaterialEigenschaftenSnapshot
AS
BEGIN
   SET NOCOUNT ON;

   SELECT
      ArrayIndex = CONVERT(int, m.ID),
      iID = CONVERT(int, m.ID),
      Mat_Name = CONVERT(nvarchar(128), COALESCE(m.MaterialName, N'')),
      Datum_Zeit = CONVERT(datetime2(0), COALESCE(m.Datum_Zeit, SYSUTCDATETIME())),
      diMasseGattMin = CONVERT(int, COALESCE(m.diMasseGattMin, 0)),
      diZeitAbtippen = CONVERT(int, COALESCE(m.diZeitAbtippen, 0)),
      diMasseVorAbtippen = CONVERT(int, COALESCE(m.diMasseVorAbtippen, 0)),
      diMasseTol_pos = CONVERT(int, COALESCE(m.diMasseTol_pos, 0)),
      diMasseTol_neg = CONVERT(int, COALESCE(m.diMasseTol_neg, 0)),
      rKraftStufenPro100Kg = CONVERT(real, COALESCE(m.rKraftStufenPro100Kg, 0)),
      xAbwurfFlach = CONVERT(bit, COALESCE(m.xAbwurfFlach, 0)),
      xAbwurfAbzett = CONVERT(bit, COALESCE(m.xAbwurfAbzett, 0)),
      xAbwurfTippen = CONVERT(bit, COALESCE(m.xAbwurfTippen, 0)),
      xNachfassenMag_Aus = CONVERT(bit, COALESCE(m.xNachfassenMag_Aus, 0)),
      xNachfassenMag_Dauernd = CONVERT(bit, COALESCE(m.xNachfassenMag_Dauernd, 0)),
      xKreislauf = CONVERT(bit, COALESCE(m.xKreislauf, 0))
   FROM dbo.FALCOM_MATERIAL AS m
   WHERE m.ID BETWEEN 1 AND 20
   ORDER BY m.ID;
END;
GO
