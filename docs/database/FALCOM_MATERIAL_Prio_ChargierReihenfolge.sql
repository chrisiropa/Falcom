USE [FG];
GO

SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'Prio') IS NULL
BEGIN
   ALTER TABLE dbo.FALCOM_MATERIAL
      ADD Prio int NOT NULL CONSTRAINT DF_FALCOM_MATERIAL_Prio DEFAULT (100);
END;
GO

UPDATE dbo.FALCOM_MATERIAL
   SET Prio =
      CASE
         WHEN xKreislauf = 1 THEN 10000
         WHEN Prio = 0 THEN 100
         ELSE Prio
      END;
GO

IF COL_LENGTH(N'dbo.FALCOM_AUFTRAG_BERECHNET', N'ChargierReihenfolge') IS NULL
BEGIN
   ALTER TABLE dbo.FALCOM_AUFTRAG_BERECHNET
      ADD ChargierReihenfolge int NULL;
END;
GO

;WITH Reihenfolge AS
(
   SELECT
      b.ID,
      ChargierReihenfolge =
         ROW_NUMBER() OVER
         (
            PARTITION BY b.AuftragID
            ORDER BY
               CASE WHEN COALESCE(l.Kreislauf, 0) = 1 OR COALESCE(m.xKreislauf, 0) = 1 THEN 0 ELSE 1 END,
               COALESCE(m.Prio, 0) DESC,
               COALESCE(b.PositionsNr, 2147483647),
               b.ID
         )
   FROM dbo.FALCOM_AUFTRAG_BERECHNET AS b
   LEFT JOIN dbo.FALCOM_LAGER AS l
      ON l.ID = b.BoxID
   LEFT JOIN dbo.FALCOM_MATERIAL AS m
      ON m.ID = l.MaterialID
   WHERE b.BerechnungAktiv = 1
     AND COALESCE(NULLIF(b.BerechnetKg, 0), NULLIF(CONVERT(decimal(18,3), b.Menge), 0), 0) > 0
)
UPDATE b
   SET ChargierReihenfolge = r.ChargierReihenfolge
FROM dbo.FALCOM_AUFTRAG_BERECHNET AS b
INNER JOIN Reihenfolge AS r
   ON r.ID = b.ID
WHERE b.ChargierReihenfolge IS NULL;
GO

IF NOT EXISTS
(
   SELECT 1
   FROM sys.indexes
   WHERE object_id = OBJECT_ID(N'dbo.FALCOM_AUFTRAG_BERECHNET')
     AND name = N'IX_FALCOM_AUFTRAG_BERECHNET_Auftrag_Reihenfolge'
)
BEGIN
   CREATE INDEX IX_FALCOM_AUFTRAG_BERECHNET_Auftrag_Reihenfolge
   ON dbo.FALCOM_AUFTRAG_BERECHNET (AuftragID, BerechnungAktiv, ChargierReihenfolge, PositionsNr, ID)
   INCLUDE (BoxID, Menge, BerechnetKg);
END;
GO
