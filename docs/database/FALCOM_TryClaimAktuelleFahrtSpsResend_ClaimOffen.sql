SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE OR ALTER PROCEDURE dbo.FALCOM_TryClaimAktuelleFahrtSpsResend
AS
BEGIN
   SET NOCOUNT ON;
   SET XACT_ABORT ON;

   BEGIN TRANSACTION;

   DECLARE @FahrtID bigint;
   SELECT TOP (1) @FahrtID = ID
   FROM dbo.FALCOM_AKTUELLE_FAHRT WITH (UPDLOCK, HOLDLOCK)
   WHERE SpsSendestatus = N'NEU_SENDEN'
      OR (
         SpsSendestatus = N'OFFEN'
         AND SpsGesendetAm IS NULL
      )
      OR (
         SpsSendestatus = N'SENDET'
         AND SpsGesendetAm IS NULL
         AND (SpsSendewunschAm IS NULL OR SpsSendewunschAm <= DATEADD(SECOND, -10, SYSDATETIME()))
      )
      OR (
         SpsSendestatus = N'FEHLER'
         AND (SpsNaechsterSendeversuchAm IS NULL OR SpsNaechsterSendeversuchAm <= SYSDATETIME())
      )
   ORDER BY ID;

   IF @FahrtID IS NULL
   BEGIN
      COMMIT TRANSACTION;
      SELECT
         CAST(0 AS bit) AS IsCurrent,
         N'Keine SPS-Neusendeanforderung vorhanden.' AS Reason,
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
         CAST(NULL AS int) AS SpsLetzterZaehlerAnfahrt;
      RETURN;
   END;

   UPDATE dbo.FALCOM_AKTUELLE_FAHRT
      SET SpsSendestatus = N'SENDET',
          SpsSendewunschAm = COALESCE(SpsSendewunschAm, SYSDATETIME()),
          SpsSendewunschVon = COALESCE(SpsSendewunschVon, N'FALCOM_AUTO'),
          SpsSendewunschGrund = COALESCE(SpsSendewunschGrund, N'Automatischer SPS-Sendeversuch fuer offene oder fehlgeschlagene aktuelle Fahrt.'),
          SpsSendefehler = NULL,
          SpsNaechsterSendeversuchAm = NULL
    WHERE ID = @FahrtID;

   COMMIT TRANSACTION;
   EXEC dbo.FALCOM_GetAktuelleFahrt;
END;
GO
