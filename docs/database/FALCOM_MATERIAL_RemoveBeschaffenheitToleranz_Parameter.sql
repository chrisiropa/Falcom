SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

MERGE dbo.FALCOM_PARAMETER AS target
USING (VALUES
   (N'MaterialSpsImportPasswort', N'31415', N'Klartext-Passwort fuer den seltenen Import der Materialeigenschaften aus SPS/OPC ueber die Material-Webseite.')
) AS source (Name, Wert, Beschreibung)
ON target.Name = source.Name
WHEN MATCHED THEN
   UPDATE SET Wert = source.Wert,
              Beschreibung = source.Beschreibung
WHEN NOT MATCHED THEN
   INSERT (Name, Wert, Beschreibung)
   VALUES (source.Name, source.Wert, source.Beschreibung);
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_GetLagerplaetze
AS
BEGIN
   SET NOCOUNT ON;

   SELECT
      lager.Lagerplatz,
      lager.Aktiv,
      lager.Restmenge,
      lager.Bezeichnung,
      COALESCE(material.MaterialName, N'') AS Schrottsorte,
      COALESCE(material.Cu, 0) AS Cu,
      COALESCE(material.Mn, 0) AS Mn,
      COALESCE(material.C, 0) AS C,
      COALESCE(material.Si, 0) AS Si,
      COALESCE(material.Cr, 0) AS S,
      COALESCE(material.Mg, 0) AS Mg,
      CONVERT(decimal(12, 3), 150) AS Toleranz
   FROM dbo.FALCOM_LAGER AS lager
   LEFT JOIN dbo.FALCOM_MATERIAL AS material
      ON material.ID = lager.MaterialID
   ORDER BY lager.Lagerplatz;
END;
GO

DECLARE @sql nvarchar(max);

SELECT @sql = STRING_AGG(N'ALTER TABLE dbo.FALCOM_MATERIAL DROP CONSTRAINT ' + QUOTENAME(dc.name), N';' + CHAR(13) + CHAR(10))
FROM sys.default_constraints AS dc
INNER JOIN sys.columns AS c
   ON c.object_id = dc.parent_object_id
  AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.FALCOM_MATERIAL')
  AND c.name IN (N'Toleranz', N'Beschaffenheit');

IF @sql IS NOT NULL
BEGIN
   EXEC sp_executesql @sql;
END;
GO

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'Toleranz') IS NOT NULL
BEGIN
   ALTER TABLE dbo.FALCOM_MATERIAL DROP COLUMN Toleranz;
END;
GO

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'Beschaffenheit') IS NOT NULL
BEGIN
   ALTER TABLE dbo.FALCOM_MATERIAL DROP COLUMN Beschaffenheit;
END;
GO
