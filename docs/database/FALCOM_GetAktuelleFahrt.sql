USE [FG]
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_GetAktuelleFahrt
AS
BEGIN
   SET NOCOUNT ON;

   IF NOT EXISTS (SELECT 1 FROM dbo.FALCOM_AKTUELLE_FAHRT)
   BEGIN
      SELECT
         CAST(0 AS bit) AS IsCurrent,
         N'Keine aktuelle Fahrt vorhanden.' AS Reason,
         CAST(NULL AS bigint) AS AktuelleFahrtID,
         CAST(NULL AS bigint) AS AuftragID,
         CAST(NULL AS nvarchar(30)) AS AuftragsTyp,
         CAST(NULL AS nvarchar(128)) AS Quelle,
         CAST(NULL AS nvarchar(128)) AS Ziel,
         CAST(NULL AS bigint) AS QuellePositionID,
         CAST(NULL AS bigint) AS ZielPositionID,
         CAST(NULL AS decimal(18,3)) AS SollMengeKg,
         CAST(NULL AS decimal(18,3)) AS IstMengeKg;

      RETURN;
   END;

   SELECT TOP (1)
      CAST(1 AS bit) AS IsCurrent,
      N'Aktuelle Fahrt aus FALCOM_AKTUELLE_FAHRT rekonstruiert.' AS Reason,
      f.ID AS AktuelleFahrtID,
      f.AuftragID,
      f.AuftragsTyp,
      q.Bezeichnung AS Quelle,
      z.Bezeichnung AS Ziel,
      f.QuellePositionID,
      f.ZielPositionID,
      CONVERT(decimal(18,3), f.SollMengeKg) AS SollMengeKg,
      CONVERT(decimal(18,3), f.IstMengeKg) AS IstMengeKg
   FROM dbo.FALCOM_AKTUELLE_FAHRT AS f
   LEFT JOIN dbo.FALCOM_KRAN_POSITION AS q ON q.ID = f.QuellePositionID
   LEFT JOIN dbo.FALCOM_KRAN_POSITION AS z ON z.ID = f.ZielPositionID
   ORDER BY f.ID;
END;
GO
