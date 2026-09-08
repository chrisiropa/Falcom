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
         CAST(NULL AS int) AS AuftragTeilfahrt,
         CAST(NULL AS nvarchar(30)) AS AuftragsTyp,
         CAST(NULL AS nvarchar(128)) AS Quelle,
         CAST(NULL AS nvarchar(128)) AS Ziel,
         CAST(NULL AS bigint) AS QuellePositionID,
         CAST(NULL AS bigint) AS ZielPositionID,
         CAST(NULL AS int) AS QuelleUnterposition,
         CAST(NULL AS int) AS ZielUnterposition,
         CAST(NULL AS decimal(18,3)) AS SollMengeKg,
         CAST(NULL AS decimal(18,3)) AS IstMengeKg,
         CAST(NULL AS nvarchar(30)) AS SpsSendestatus,
         CAST(NULL AS datetime2(3)) AS SpsSendewunschAm,
         CAST(NULL AS nvarchar(128)) AS SpsSendewunschVon,
         CAST(NULL AS nvarchar(512)) AS SpsSendewunschGrund,
         CAST(NULL AS datetime2(3)) AS SpsGesendetAm,
         CAST(NULL AS nvarchar(1024)) AS SpsSendefehler,
         CAST(NULL AS int) AS SpsLetzteTelegrammNummer,
         CAST(NULL AS int) AS SpsLetzterZaehlerAnfahrt,
         CAST(NULL AS int) AS SpsRueckmeldeStatus,
         CAST(NULL AS datetime2(3)) AS SpsRueckmeldungAm;

      RETURN;
   END;

   SELECT TOP (1)
      CAST(1 AS bit) AS IsCurrent,
      N'Aktuelle Fahrt aus FALCOM_AKTUELLE_FAHRT rekonstruiert.' AS Reason,
      f.ID AS AktuelleFahrtID,
      f.AuftragID,
      f.AuftragTeilfahrt,
      f.AuftragsTyp,
      q.Bezeichnung AS Quelle,
      z.Bezeichnung AS Ziel,
      f.QuellePositionID,
      f.ZielPositionID,
      f.QuelleUnterposition,
      f.ZielUnterposition,
      CONVERT(decimal(18,3), f.SollMengeKg) AS SollMengeKg,
      CONVERT(decimal(18,3), f.IstMengeKg) AS IstMengeKg,
      f.SpsSendestatus,
      f.SpsSendewunschAm,
      f.SpsSendewunschVon,
      f.SpsSendewunschGrund,
      f.SpsGesendetAm,
      f.SpsSendefehler,
      f.SpsLetzteTelegrammNummer,
      f.SpsLetzterZaehlerAnfahrt,
      f.SpsRueckmeldeStatus,
      f.SpsRueckmeldungAm
   FROM dbo.FALCOM_AKTUELLE_FAHRT AS f
   LEFT JOIN dbo.FALCOM_KRAN_POSITION AS q ON q.ID = f.QuellePositionID
   LEFT JOIN dbo.FALCOM_KRAN_POSITION AS z ON z.ID = f.ZielPositionID
   ORDER BY f.ID;
END;
GO
