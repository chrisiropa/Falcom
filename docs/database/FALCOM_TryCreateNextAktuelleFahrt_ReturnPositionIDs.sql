USE [FG]
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_TryCreateNextAktuelleFahrt
   @AuftragID bigint = NULL
AS
BEGIN
   SET NOCOUNT ON;
   SET XACT_ABORT ON;

   DECLARE @Result table
   (
      Created bit NOT NULL,
      Reason nvarchar(200) NOT NULL,
      AktuelleFahrtID bigint NULL,
      AuftragID bigint NULL,
      AuftragsTyp nvarchar(30) NULL,
      Quelle nvarchar(128) NULL,
      Ziel nvarchar(128) NULL,
      QuellePositionID bigint NULL,
      ZielPositionID bigint NULL,
      SollMengeKg decimal(18,3) NULL
   );

   BEGIN TRANSACTION;

   IF EXISTS (SELECT 1 FROM dbo.FALCOM_AKTUELLE_FAHRT WITH (UPDLOCK, HOLDLOCK))
   BEGIN
      INSERT INTO @Result(Created, Reason, AktuelleFahrtID, AuftragID, AuftragsTyp, Quelle, Ziel, QuellePositionID, ZielPositionID, SollMengeKg)
      SELECT TOP (1)
         CAST(0 AS bit),
         N'FALCOM_AKTUELLE_FAHRT ist bereits belegt.',
         f.ID,
         f.AuftragID,
         f.AuftragsTyp,
         q.Bezeichnung,
         z.Bezeichnung,
         f.QuellePositionID,
         f.ZielPositionID,
         f.SollMengeKg
      FROM dbo.FALCOM_AKTUELLE_FAHRT AS f
      LEFT JOIN dbo.FALCOM_KRAN_POSITION AS q ON q.ID = f.QuellePositionID
      LEFT JOIN dbo.FALCOM_KRAN_POSITION AS z ON z.ID = f.ZielPositionID
      ORDER BY f.ID;

      COMMIT TRANSACTION;
      SELECT * FROM @Result;
      RETURN;
   END;

   DECLARE @EinlagerID bigint;
   DECLARE @LkwPlatzNr int;
   DECLARE @ZielBoxID bigint;
   DECLARE @EinlagerSoll decimal(18,3);
   DECLARE @QuellePositionID bigint;
   DECLARE @ZielPositionID bigint;
   DECLARE @NeueFahrtID table (ID bigint NOT NULL);

   IF @AuftragID IS NULL
   BEGIN
      SELECT TOP (1)
         @EinlagerID = e.ID,
         @LkwPlatzNr = e.LkwPlatzNr,
         @ZielBoxID = e.ZielBoxID,
         @EinlagerSoll = e.SollMengeKg
      FROM dbo.FALCOM_EINLAGER_AUFTRAG AS e WITH (UPDLOCK, HOLDLOCK)
      WHERE e.DatumZeitFertig IS NULL
      ORDER BY e.Reihenfolge ASC, e.ID ASC;
   END;

   IF @EinlagerID IS NOT NULL
   BEGIN
      SELECT @QuellePositionID = p.ID
      FROM dbo.FALCOM_KRAN_POSITION AS p
      WHERE p.PositionsTyp = N'LKW_PLATZ'
        AND p.PositionsNr = @LkwPlatzNr;

      SELECT @ZielPositionID = p.ID
      FROM dbo.FALCOM_KRAN_POSITION AS p
      WHERE p.ID = @ZielBoxID;

      INSERT INTO dbo.FALCOM_AKTUELLE_FAHRT
      (
         AuftragsTyp,
         AuftragID,
         Bemerkung,
         Status,
         QuellePositionID,
         ZielPositionID,
         SollMengeKg
      )
      OUTPUT inserted.ID INTO @NeueFahrtID(ID)
      VALUES
      (
         N'EINLAGERN',
         @EinlagerID,
         N'Automatisch aus offenem Einlagerauftrag erzeugt.',
         N'OFFEN',
         @QuellePositionID,
         @ZielPositionID,
         @EinlagerSoll
      );

      INSERT INTO @Result(Created, Reason, AktuelleFahrtID, AuftragID, AuftragsTyp, Quelle, Ziel, QuellePositionID, ZielPositionID, SollMengeKg)
      SELECT
         CAST(1 AS bit),
         N'Aktuelle Einlagerfahrt erzeugt.',
         nf.ID,
         @EinlagerID,
         N'EINLAGERN',
         q.Bezeichnung,
         z.Bezeichnung,
         @QuellePositionID,
         @ZielPositionID,
         @EinlagerSoll
      FROM @NeueFahrtID AS nf
      LEFT JOIN dbo.FALCOM_KRAN_POSITION AS q ON q.ID = @QuellePositionID
      LEFT JOIN dbo.FALCOM_KRAN_POSITION AS z ON z.ID = @ZielPositionID;

      COMMIT TRANSACTION;
      SELECT * FROM @Result;
      RETURN;
   END;

   DECLARE @ChargierAuftragID bigint;
   DECLARE @ChargierwagenNr int;
   DECLARE @ZielgewichtKg decimal(18,3);
   DECLARE @QuellBoxID bigint;
   DECLARE @QuellSollMengeKg decimal(18,3);

   SELECT TOP (1)
      @ChargierAuftragID = a.ID,
      @ChargierwagenNr = a.ChargierwagenNr,
      @ZielgewichtKg = CONVERT(decimal(18,3), a.ZielgewichtKg)
   FROM dbo.FALCOM_AUFTRAG AS a WITH (UPDLOCK, HOLDLOCK)
   WHERE (@AuftragID IS NULL OR a.ID = @AuftragID)
     AND a.Status IN (N'FREIGEGEBEN', N'IN_ARBEIT')
     AND a.Gesperrt = 0
   ORDER BY a.EingabeDatumZeit ASC, a.ID ASC;

   IF @ChargierAuftragID IS NULL
   BEGIN
      INSERT INTO @Result(Created, Reason)
      VALUES (CAST(0 AS bit), N'Kein freigegebener oder aktiver Auftrag gefunden.');

      COMMIT TRANSACTION;
      SELECT * FROM @Result;
      RETURN;
   END;

   SELECT TOP (1)
      @QuellBoxID = b.BoxID,
      @QuellSollMengeKg = CONVERT(decimal(18,3), COALESCE(NULLIF(b.BerechnetKg, 0), NULLIF(CONVERT(decimal(18,3), b.Menge), 0)))
   FROM dbo.FALCOM_AUFTRAG_BERECHNET AS b WITH (UPDLOCK, HOLDLOCK)
   WHERE b.AuftragID = @ChargierAuftragID
     AND b.BerechnungAktiv = 1
     AND COALESCE(NULLIF(b.BerechnetKg, 0), NULLIF(CONVERT(decimal(18,3), b.Menge), 0), 0) > 0
   ORDER BY b.PositionsNr ASC, b.ID ASC;

   IF @QuellBoxID IS NULL
   BEGIN
      INSERT INTO @Result(Created, Reason, AuftragID, AuftragsTyp)
      VALUES (CAST(0 AS bit), N'Für den Auftrag wurde keine aktive berechnete Quelle mit Menge > 0 gefunden.', @ChargierAuftragID, N'CHARGIEREN');

      COMMIT TRANSACTION;
      SELECT * FROM @Result;
      RETURN;
   END;

   SELECT @QuellePositionID = p.ID
   FROM dbo.FALCOM_KRAN_POSITION AS p
   WHERE (p.PositionsTyp = N'LAGERBOX' AND p.PositionsNr = @QuellBoxID)
      OR (p.PositionsTyp = N'LKW_PLATZ' AND @QuellBoxID BETWEEN 101 AND 103 AND p.PositionsNr = @QuellBoxID - 100);

   SELECT @ZielPositionID = p.ID
   FROM dbo.FALCOM_KRAN_POSITION AS p
   WHERE p.PositionsTyp = N'CHARGIERWAGEN'
     AND p.PositionsNr = @ChargierwagenNr;

   IF @QuellePositionID IS NULL OR @ZielPositionID IS NULL
   BEGIN
      INSERT INTO @Result(Created, Reason, AuftragID, AuftragsTyp, SollMengeKg)
      VALUES (CAST(0 AS bit), N'Quelle oder Ziel konnte nicht auf FALCOM_KRAN_POSITION abgebildet werden.', @ChargierAuftragID, N'CHARGIEREN', @QuellSollMengeKg);

      COMMIT TRANSACTION;
      SELECT * FROM @Result;
      RETURN;
   END;

   UPDATE dbo.FALCOM_AUFTRAG
      SET Status = N'IN_ARBEIT',
          BearbeitungGestartet = 1,
          StartDatumZeit = COALESCE(StartDatumZeit, SYSDATETIME()),
          GeaendertDatumZeit = SYSDATETIME()
    WHERE ID = @ChargierAuftragID;

   INSERT INTO dbo.FALCOM_AKTUELLE_FAHRT
   (
      AuftragsTyp,
      AuftragID,
      Bemerkung,
      Status,
      QuellePositionID,
      ZielPositionID,
      SollMengeKg
   )
   OUTPUT inserted.ID INTO @NeueFahrtID(ID)
   VALUES
   (
      N'CHARGIEREN',
      @ChargierAuftragID,
      N'Automatisch aus freigegebenem Chargierauftrag und FALCOM_AUFTRAG_BERECHNET erzeugt.',
      N'OFFEN',
      @QuellePositionID,
      @ZielPositionID,
      @QuellSollMengeKg
   );

   INSERT INTO @Result(Created, Reason, AktuelleFahrtID, AuftragID, AuftragsTyp, Quelle, Ziel, QuellePositionID, ZielPositionID, SollMengeKg)
   SELECT
      CAST(1 AS bit),
      N'Aktuelle Chargierfahrt erzeugt.',
      nf.ID,
      @ChargierAuftragID,
      N'CHARGIEREN',
      q.Bezeichnung,
      z.Bezeichnung,
      @QuellePositionID,
      @ZielPositionID,
      @QuellSollMengeKg
   FROM @NeueFahrtID AS nf
   LEFT JOIN dbo.FALCOM_KRAN_POSITION AS q ON q.ID = @QuellePositionID
   LEFT JOIN dbo.FALCOM_KRAN_POSITION AS z ON z.ID = @ZielPositionID;

   COMMIT TRANSACTION;
   SELECT * FROM @Result;
END;
GO
