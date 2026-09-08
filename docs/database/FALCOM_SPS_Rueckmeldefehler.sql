USE [FG]
GO

/*
   Event 202 mit Status <> 0 ist keine erfolgreich abgeschlossene Fahrt.
   Die aktuelle Fahrt bleibt deshalb im Ein-Fahrt-Slot gesperrt, bis der
   Bediener sie explizit wiederholt oder ueber die vorhandene Abbruchfunktion
   beendet.
*/
IF COL_LENGTH(N'dbo.FALCOM_AKTUELLE_FAHRT', N'SpsRueckmeldeStatus') IS NULL
BEGIN
   ALTER TABLE dbo.FALCOM_AKTUELLE_FAHRT
      ADD SpsRueckmeldeStatus int NULL;
END;
GO

IF COL_LENGTH(N'dbo.FALCOM_AKTUELLE_FAHRT', N'SpsRueckmeldungAm') IS NULL
BEGIN
   ALTER TABLE dbo.FALCOM_AKTUELLE_FAHRT
      ADD SpsRueckmeldungAm datetime2(3) NULL;
END;
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_HoldAktuelleFahrtNachSpsRueckmeldefehler
   @AuftragsNummer bigint,
   @AuftragTeilfahrt bigint,
   @Status int,
   @AenderungsZaehler int = NULL
AS
BEGIN
   SET NOCOUNT ON;
   SET XACT_ABORT ON;

   DECLARE @Jetzt datetime2(3) = SYSDATETIME();
   DECLARE @FahrtID bigint = NULL;
   DECLARE @AktuelleAuftragID bigint = NULL;
   DECLARE @AktuelleTeilfahrt int = NULL;
   DECLARE @Reason nvarchar(512);

   BEGIN TRY
      BEGIN TRANSACTION;

      SELECT TOP (1)
         @FahrtID = f.ID,
         @AktuelleAuftragID = f.AuftragID,
         @AktuelleTeilfahrt = f.AuftragTeilfahrt
      FROM dbo.FALCOM_AKTUELLE_FAHRT AS f WITH (UPDLOCK, HOLDLOCK)
      ORDER BY f.ID;

      IF @FahrtID IS NULL
      BEGIN
         ROLLBACK TRANSACTION;
         SELECT CAST(0 AS bit) AS Success,
                N'Event 202 mit Fehlerstatus ignoriert: Es gibt keine aktuelle Fahrt.' AS Reason;
         RETURN;
      END;

      IF @AktuelleAuftragID <> @AuftragsNummer OR @AktuelleTeilfahrt <> @AuftragTeilfahrt
      BEGIN
         ROLLBACK TRANSACTION;
         SELECT CAST(0 AS bit) AS Success,
                LEFT(CONCAT(N'Event 202 passt nicht zur aktuellen Fahrt. Erwartet Auftrag ', @AktuelleAuftragID,
                            N', Teilfahrt ', @AktuelleTeilfahrt, N'; erhalten Auftrag ', @AuftragsNummer,
                            N', Teilfahrt ', @AuftragTeilfahrt, N'.'), 512) AS Reason;
         RETURN;
      END;

      SET @Reason = LEFT(CONCAT(N'SPS meldet Event 202 mit Status ', @Status,
                                N'. Teilfahrt bleibt gesperrt; Bedienerentscheidung erforderlich.'), 512);

      UPDATE dbo.FALCOM_AKTUELLE_FAHRT
         SET SpsSendestatus = N'SPS_RUECKMELDEFEHLER',
             SpsRueckmeldeStatus = @Status,
             SpsRueckmeldungAm = @Jetzt,
             SpsSendefehler = @Reason,
             SpsNaechsterSendeversuchAm = NULL
       WHERE ID = @FahrtID;

      INSERT INTO dbo.FALCOM_KRAN_COMMAND_LOG
      (
         AktuelleFahrtID, DatumZeit, Modus, SollMengeKg, IstMengeKg, Meldung, Erfolgreich
      )
      SELECT
         f.ID, @Jetzt, N'SPS_EVENT_202_FEHLER', f.SollMengeKg, f.IstMengeKg,
         LEFT(CONCAT(@Reason, N' AenderungsZaehler=', COALESCE(CONVERT(nvarchar(20), @AenderungsZaehler), N'-')), 1000),
         CAST(0 AS bit)
      FROM dbo.FALCOM_AKTUELLE_FAHRT AS f
      WHERE f.ID = @FahrtID;

      COMMIT TRANSACTION;
      SELECT CAST(1 AS bit) AS Success, @Reason AS Reason;
   END TRY
   BEGIN CATCH
      IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
      SELECT CAST(0 AS bit) AS Success,
             LEFT(CONCAT(N'Fehlerstatus aus Event 202 konnte nicht gespeichert werden: ', ERROR_MESSAGE()), 512) AS Reason;
   END CATCH;
END;
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_RequestAktuelleFahrtWiederholen
   @AktuelleFahrtID bigint,
   @Grund nvarchar(512) = NULL
AS
BEGIN
   SET NOCOUNT ON;
   SET XACT_ABORT ON;

   DECLARE @Jetzt datetime2(3) = SYSDATETIME();
   DECLARE @AuftragID bigint = NULL;
   DECLARE @Teilfahrt int = NULL;
   DECLARE @Reason nvarchar(512) = LEFT(COALESCE(NULLIF(@Grund, N''), N'Bedieneranforderung: Teilfahrt mit identischer Teilnummer wiederholen.'), 512);

   BEGIN TRY
      BEGIN TRANSACTION;

      SELECT
         @AuftragID = AuftragID,
         @Teilfahrt = AuftragTeilfahrt
      FROM dbo.FALCOM_AKTUELLE_FAHRT WITH (UPDLOCK, HOLDLOCK)
      WHERE ID = @AktuelleFahrtID
        AND SpsSendestatus = N'SPS_RUECKMELDEFEHLER'
        AND SpsRueckmeldeStatus IS NOT NULL
        AND SpsRueckmeldeStatus <> 0;

      IF @AuftragID IS NULL
      BEGIN
         ROLLBACK TRANSACTION;
         SELECT CAST(0 AS bit) AS Success,
                N'Die Teilfahrt ist nicht mehr als SPS-Rueckmeldefehler gesperrt und kann daher nicht wiederholt werden.' AS Reason;
         RETURN;
      END;

      UPDATE dbo.FALCOM_AKTUELLE_FAHRT
         SET SpsSendestatus = N'NEU_SENDEN',
             SpsSendewunschAm = @Jetzt,
             SpsSendewunschVon = N'WEB_FALCOM_KRANFAHRTEN',
             SpsSendewunschGrund = @Reason,
             SpsSendefehler = NULL,
             SpsNaechsterSendeversuchAm = NULL,
             SpsRueckmeldeStatus = NULL,
             SpsRueckmeldungAm = NULL
       WHERE ID = @AktuelleFahrtID;

      INSERT INTO dbo.FALCOM_KRAN_COMMAND_LOG
      (
         AktuelleFahrtID, DatumZeit, Modus, SollMengeKg, IstMengeKg, Meldung, Erfolgreich
      )
      SELECT
         f.ID, @Jetzt, N'TEILFAHRT_WIEDERHOLEN', f.SollMengeKg, f.IstMengeKg,
         LEFT(CONCAT(@Reason, N' Auftrag=', @AuftragID, N', Teilfahrt=', @Teilfahrt, N'.'), 1000),
         CAST(1 AS bit)
      FROM dbo.FALCOM_AKTUELLE_FAHRT AS f
      WHERE f.ID = @AktuelleFahrtID;

      COMMIT TRANSACTION;
      SELECT CAST(1 AS bit) AS Success,
             LEFT(CONCAT(N'Teilfahrt ', @Teilfahrt, N' wurde zur Wiederholung vorgemerkt.'), 512) AS Reason;
   END TRY
   BEGIN CATCH
      IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
      SELECT CAST(0 AS bit) AS Success,
             LEFT(CONCAT(N'Teilfahrt konnte nicht zur Wiederholung vorgemerkt werden: ', ERROR_MESSAGE()), 512) AS Reason;
   END CATCH;
END;
GO
