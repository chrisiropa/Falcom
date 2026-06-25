USE [FG]
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_FailAktuelleFahrt
   @AktuelleFahrtID bigint = NULL,
   @Bemerkung nvarchar(1024)
AS
BEGIN
   SET NOCOUNT ON;
   SET XACT_ABORT ON;

   DECLARE @Result table
   (
      Completed bit NOT NULL,
      Reason nvarchar(300) NOT NULL,
      AktuelleFahrtID bigint NULL,
      AuftragID bigint NULL,
      AuftragsTyp nvarchar(30) NULL,
      Quelle nvarchar(128) NULL,
      Ziel nvarchar(128) NULL,
      SollMengeKg decimal(18,3) NULL,
      IstMengeKg decimal(18,3) NULL
   );

   BEGIN TRANSACTION;

   DECLARE @FahrtID bigint;
   DECLARE @AuftragsTyp nvarchar(30);
   DECLARE @AuftragID bigint;
   DECLARE @ErstelltDatumZeit datetime;
   DECLARE @GestartetDatumZeit datetime2;
   DECLARE @QuellePositionID bigint;
   DECLARE @ZielPositionID bigint;
   DECLARE @SollMengeKg decimal(18,3);
   DECLARE @Quelle nvarchar(128);
   DECLARE @Ziel nvarchar(128);
   DECLARE @FertigDatumZeit datetime = SYSDATETIME();

   SELECT TOP (1)
      @FahrtID = f.ID,
      @AuftragsTyp = f.AuftragsTyp,
      @AuftragID = f.AuftragID,
      @ErstelltDatumZeit = f.ErstelltDatumZeit,
      @GestartetDatumZeit = f.GestartetDatumZeit,
      @QuellePositionID = f.QuellePositionID,
      @ZielPositionID = f.ZielPositionID,
      @SollMengeKg = f.SollMengeKg,
      @Quelle = q.Bezeichnung,
      @Ziel = z.Bezeichnung
   FROM dbo.FALCOM_AKTUELLE_FAHRT AS f WITH (UPDLOCK, HOLDLOCK)
   LEFT JOIN dbo.FALCOM_KRAN_POSITION AS q ON q.ID = f.QuellePositionID
   LEFT JOIN dbo.FALCOM_KRAN_POSITION AS z ON z.ID = f.ZielPositionID
   WHERE @AktuelleFahrtID IS NULL OR f.ID = @AktuelleFahrtID
   ORDER BY f.ID;

   IF @FahrtID IS NULL
   BEGIN
      INSERT INTO @Result(Completed, Reason)
      VALUES (CAST(0 AS bit), N'Keine aktuelle Fahrt zur Fehler-Historisierung gefunden.');

      COMMIT TRANSACTION;
      SELECT * FROM @Result;
      RETURN;
   END;

   UPDATE dbo.FALCOM_AKTUELLE_FAHRT
      SET Status = N'FEHLER',
          FertigDatumZeit = @FertigDatumZeit,
          Bemerkung = LEFT(@Bemerkung, 500)
    WHERE ID = @FahrtID;

   IF @AuftragsTyp = N'CHARGIEREN'
   BEGIN
      DECLARE @PositionsNr int;
      DECLARE @QuellTyp nvarchar(30) = N'LAGERBOX';
      DECLARE @QuellID bigint;

      SELECT TOP (1)
         @PositionsNr = b.PositionsNr,
         @QuellID = b.BoxID
      FROM dbo.FALCOM_AUFTRAG_BERECHNET AS b
      LEFT JOIN dbo.FALCOM_KRAN_POSITION AS q ON q.ID = @QuellePositionID
      WHERE b.AuftragID = @AuftragID
        AND b.BerechnungAktiv = 1
        AND (
              b.BoxID = q.PositionsNr
              OR b.BoxID = q.PositionsNr + 100
            )
      ORDER BY b.PositionsNr ASC, b.ID ASC;

      IF @PositionsNr IS NULL
      BEGIN
         SELECT @PositionsNr = COALESCE(MAX(p.PositionsNr), 0) + 1
         FROM dbo.FALCOM_AUFTRAG_PRODUKTION AS p WITH (UPDLOCK, HOLDLOCK)
         WHERE p.AuftragID = @AuftragID;
      END;

      IF @QuellID IS NULL
      BEGIN
         SELECT @QuellID = COALESCE(q.PositionsNr, @QuellePositionID, 0)
         FROM dbo.FALCOM_KRAN_POSITION AS q
         WHERE q.ID = @QuellePositionID;
      END;

      INSERT INTO dbo.FALCOM_AUFTRAG_PRODUKTION
      (
         AuftragID,
         PositionsNr,
         QuellTyp,
         QuellID,
         SollMengeKg,
         Status,
         ErstelltDatumZeit,
         StartDatumZeit,
         FertigDatumZeit,
         Bemerkung
      )
      VALUES
      (
         @AuftragID,
         @PositionsNr,
         @QuellTyp,
         COALESCE(@QuellID, 0),
         COALESCE(@SollMengeKg, 0),
         N'FEHLER',
         COALESCE(@ErstelltDatumZeit, @FertigDatumZeit),
         COALESCE(CONVERT(datetime, @GestartetDatumZeit), @FertigDatumZeit),
         @FertigDatumZeit,
         LEFT(@Bemerkung, 1024)
      );

      UPDATE dbo.FALCOM_AUFTRAG
         SET Status = N'FEHLER',
             FertigDatumZeit = COALESCE(FertigDatumZeit, @FertigDatumZeit),
             GeaendertDatumZeit = @FertigDatumZeit,
             Bemerkung = LEFT(CONCAT(
                COALESCE(NULLIF(Bemerkung, N''), N''),
                CASE WHEN Bemerkung IS NULL OR Bemerkung = N'' THEN N'' ELSE N' | ' END,
                @Bemerkung), 1024)
       WHERE ID = @AuftragID;
   END;

   IF @AuftragsTyp = N'EINLAGERN'
   BEGIN
      UPDATE dbo.FALCOM_EINLAGER_AUFTRAG
         SET DatumZeitFertig = COALESCE(DatumZeitFertig, @FertigDatumZeit),
             Bemerkung = LEFT(CONCAT(
                COALESCE(NULLIF(Bemerkung, N''), N''),
                CASE WHEN Bemerkung IS NULL OR Bemerkung = N'' THEN N'' ELSE N' | ' END,
                @Bemerkung), 500)
       WHERE ID = @AuftragID;
   END;

   DELETE FROM dbo.FALCOM_AKTUELLE_FAHRT
    WHERE ID = @FahrtID;

   INSERT INTO @Result
   (
      Completed,
      Reason,
      AktuelleFahrtID,
      AuftragID,
      AuftragsTyp,
      Quelle,
      Ziel,
      SollMengeKg,
      IstMengeKg
   )
   VALUES
   (
      CAST(1 AS bit),
      N'Aktuelle Fahrt wegen Fehler historisiert und Slot geleert.',
      @FahrtID,
      @AuftragID,
      @AuftragsTyp,
      @Quelle,
      @Ziel,
      @SollMengeKg,
      NULL
   );

   COMMIT TRANSACTION;
   SELECT * FROM @Result;
END;
GO
