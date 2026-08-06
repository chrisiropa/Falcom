SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF COL_LENGTH(N'dbo.FALCOM_AUFTRAG_PRODUKTION', N'GrundFahrtende') IS NULL
BEGIN
   ALTER TABLE dbo.FALCOM_AUFTRAG_PRODUKTION
      ADD GrundFahrtende nvarchar(30) NULL;
END;
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_RequestChargierauftragManuellBeenden
   @AuftragID bigint,
   @Grund nvarchar(512) = NULL
AS
BEGIN
   SET NOCOUNT ON;
   SET XACT_ABORT ON;

   DECLARE @Jetzt datetime2(3) = SYSDATETIME();
   DECLARE @JetztDatetime datetime = CONVERT(datetime, @Jetzt);
   DECLARE @GrundText nvarchar(512) = LEFT(COALESCE(NULLIF(@Grund, N''), N'Bediener hat Chargierauftrag manuell beendet.'), 512);
   DECLARE @AktuelleFahrtID bigint = NULL;
   DECLARE @QuellePositionID bigint = NULL;
   DECLARE @ZielPositionID bigint = NULL;
   DECLARE @AuftragTeilfahrt int = NULL;
   DECLARE @StartDatumZeit datetime = NULL;
   DECLARE @SollMengeKg decimal(12,3) = NULL;
   DECLARE @ProduktionID bigint = NULL;
   DECLARE @DetailTeilfahrtNr int = NULL;
   DECLARE @Reason nvarchar(300) = N'Chargierauftrag wurde manuell beendet und fuer weitere Fahrten gesperrt.';

   DECLARE @Analyse_Mn decimal(10,5), @Analyse_Cu decimal(10,5), @Analyse_C decimal(10,5),
           @Analyse_Si decimal(10,5), @Analyse_Cr decimal(10,5), @Analyse_Mg decimal(10,5);

   BEGIN TRY
      BEGIN TRANSACTION;

      SELECT TOP (1)
         @Analyse_Mn = a.Soll_Mn,
         @Analyse_Cu = a.Soll_Cu,
         @Analyse_C = a.Soll_C,
         @Analyse_Si = a.Soll_Si,
         @Analyse_Cr = a.Soll_Cr,
         @Analyse_Mg = a.Soll_Mg
      FROM dbo.FALCOM_AUFTRAG AS a WITH (UPDLOCK, HOLDLOCK)
      WHERE a.ID = @AuftragID;

      IF @@ROWCOUNT = 0
      BEGIN
         ROLLBACK TRANSACTION;
         SELECT CAST(0 AS bit) AS Success, N'Chargierauftrag wurde nicht gefunden.' AS Reason, @AuftragID AS AuftragID, CAST(NULL AS bigint) AS AktuelleFahrtID;
         RETURN;
      END;

      SELECT TOP (1)
         @AktuelleFahrtID = f.ID,
         @QuellePositionID = f.QuellePositionID,
         @ZielPositionID = f.ZielPositionID,
         @AuftragTeilfahrt = f.AuftragTeilfahrt,
         @StartDatumZeit = CONVERT(datetime, COALESCE(f.GestartetDatumZeit, CONVERT(datetime2(3), f.ErstelltDatumZeit))),
         @SollMengeKg = CONVERT(decimal(12,3), COALESCE(f.SollMengeKg, 0))
      FROM dbo.FALCOM_AKTUELLE_FAHRT AS f WITH (UPDLOCK, HOLDLOCK)
      WHERE f.AuftragsTyp = N'CHARGIEREN'
        AND f.AuftragID = @AuftragID
      ORDER BY f.ID;

      IF @AktuelleFahrtID IS NOT NULL AND (@QuellePositionID IS NULL OR @ZielPositionID IS NULL)
      BEGIN
         ROLLBACK TRANSACTION;
         SELECT CAST(0 AS bit) AS Success, N'Quelle oder Ziel der aktuellen Chargierfahrt fehlt.' AS Reason, @AuftragID AS AuftragID, @AktuelleFahrtID AS AktuelleFahrtID;
         RETURN;
      END;

      IF @AktuelleFahrtID IS NOT NULL
      BEGIN
         SELECT TOP (1) @ProduktionID = p.ID
         FROM dbo.FALCOM_AUFTRAG_PRODUKTION AS p WITH (UPDLOCK, HOLDLOCK)
         WHERE p.AuftragID = @AuftragID
           AND p.QuelleKranPositionID = @QuellePositionID
           AND p.ZielKranPositionID = @ZielPositionID
         ORDER BY CASE WHEN p.Status = N'IN_ARBEIT' THEN 0 ELSE 1 END, p.ID DESC;

         IF @ProduktionID IS NULL
         BEGIN
            INSERT INTO dbo.FALCOM_AUFTRAG_PRODUKTION
            (
               AuftragID, SollMengeKg, Status, ErstelltDatumZeit, StartDatumZeit, FertigDatumZeit, Bemerkung,
               QuelleKranPositionID, ZielKranPositionID,
               Analyse_Mn, Analyse_Cu, Analyse_C, Analyse_Si, Analyse_Cr, Analyse_Mg,
               GrundFahrtende
            )
            VALUES
            (
               @AuftragID, COALESCE(@SollMengeKg, 0), N'FERTIG', @JetztDatetime, COALESCE(@StartDatumZeit, @JetztDatetime), @JetztDatetime,
               LEFT(CONCAT(N'Aktiver Chargierauftrag ohne SPS-Rueckmeldung manuell beendet. ', @GrundText), 1024),
               @QuellePositionID, @ZielPositionID,
               @Analyse_Mn, @Analyse_Cu, @Analyse_C, @Analyse_Si, @Analyse_Cr, @Analyse_Mg,
               N'MANUELL'
            );

            SET @ProduktionID = CONVERT(bigint, SCOPE_IDENTITY());
         END
         ELSE
         BEGIN
            UPDATE dbo.FALCOM_AUFTRAG_PRODUKTION
            SET Status = N'FERTIG',
                StartDatumZeit = COALESCE(StartDatumZeit, COALESCE(@StartDatumZeit, @JetztDatetime)),
                FertigDatumZeit = COALESCE(FertigDatumZeit, @JetztDatetime),
                Bemerkung = LEFT(CONCAT(COALESCE(NULLIF(Bemerkung, N''), N''), CASE WHEN NULLIF(Bemerkung, N'') IS NULL THEN N'' ELSE N' | ' END, N'Chargierauftrag manuell beendet. ', @GrundText), 1024),
                GrundFahrtende = N'MANUELL'
            WHERE ID = @ProduktionID;
         END;

         SELECT @DetailTeilfahrtNr = COALESCE(@AuftragTeilfahrt, MAX(d.TeilfahrtNr) + 1, 1)
         FROM dbo.FALCOM_AUFTRAG_PRODUKTION_DETAIL AS d WITH (UPDLOCK, HOLDLOCK)
         WHERE d.ProduktionID = @ProduktionID;

         INSERT INTO dbo.FALCOM_AUFTRAG_PRODUKTION_DETAIL
         (
            ProduktionID, IstMengeKg, TeilfahrtNr, StartDatumZeit, FertigDatumZeit
         )
         VALUES
         (
            @ProduktionID, 0, COALESCE(@DetailTeilfahrtNr, 1), COALESCE(@StartDatumZeit, @JetztDatetime), @JetztDatetime
         );

         DELETE FROM dbo.FALCOM_AKTUELLE_FAHRT
         WHERE ID = @AktuelleFahrtID;
      END
      ELSE
      BEGIN
         SET @Reason = N'Chargierauftrag wurde manuell beendet. Es war keine aktuelle Chargierfahrt mehr im Slot.';
      END;

      UPDATE dbo.FALCOM_AUFTRAG_BERECHNET
      SET BerechnungAktiv = 0
      WHERE AuftragID = @AuftragID
        AND BerechnungAktiv = 1;

      UPDATE dbo.FALCOM_AUFTRAG_PRODUKTION
      SET Status = N'FERTIG',
          FertigDatumZeit = COALESCE(FertigDatumZeit, @JetztDatetime),
          Bemerkung = LEFT(CONCAT(COALESCE(NULLIF(Bemerkung, N''), N''), CASE WHEN NULLIF(Bemerkung, N'') IS NULL THEN N'' ELSE N' | ' END, N'Chargierauftrag manuell beendet. ', @GrundText), 1024),
          GrundFahrtende = COALESCE(GrundFahrtende, N'MANUELL')
      WHERE AuftragID = @AuftragID
        AND Status <> N'FERTIG';

      UPDATE dbo.FALCOM_AUFTRAG
      SET Status = N'FERTIG',
          FertigDatumZeit = COALESCE(FertigDatumZeit, @JetztDatetime),
          GeaendertDatumZeit = @JetztDatetime,
          Bemerkung = LEFT(CONCAT(COALESCE(NULLIF(Bemerkung, N''), N''), CASE WHEN NULLIF(Bemerkung, N'') IS NULL THEN N'' ELSE N' | ' END, N'Chargierauftrag manuell beendet. ', @GrundText), 1024)
      WHERE ID = @AuftragID;

      INSERT INTO dbo.FALCOM_KRAN_COMMAND_LOG
      (
         AktuelleFahrtID, DatumZeit, Modus, Quelle, Ziel, SollMengeKg, IstMengeKg, Meldung, Erfolgreich
      )
      VALUES
      (
         COALESCE(@AktuelleFahrtID, 0), @Jetzt, N'CHARGIER_MANUELL_BEENDET',
         CONVERT(nvarchar(128), @QuellePositionID), CONVERT(nvarchar(128), @ZielPositionID),
         @SollMengeKg, 0, LEFT(CONCAT(@Reason, N' ', @GrundText), 1000), CAST(1 AS bit)
      );

      COMMIT TRANSACTION;
      SELECT CAST(1 AS bit) AS Success, @Reason AS Reason, @AuftragID AS AuftragID, @AktuelleFahrtID AS AktuelleFahrtID;
   END TRY
   BEGIN CATCH
      IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
      SELECT CAST(0 AS bit) AS Success,
             LEFT(CONCAT(N'Chargierauftrag konnte nicht manuell beendet werden: ', ERROR_MESSAGE()), 300) AS Reason,
             @AuftragID AS AuftragID,
             @AktuelleFahrtID AS AktuelleFahrtID;
   END CATCH;
END;
GO

