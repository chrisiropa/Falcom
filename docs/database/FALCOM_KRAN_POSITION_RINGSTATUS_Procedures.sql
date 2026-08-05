USE [FG]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
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
         CAST(NULL AS int) AS SpsLetzterZaehlerAnfahrt;
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
      f.SpsLetzterZaehlerAnfahrt
   FROM dbo.FALCOM_AKTUELLE_FAHRT AS f
   LEFT JOIN dbo.FALCOM_KRAN_POSITION AS q ON q.ID = f.QuellePositionID
   LEFT JOIN dbo.FALCOM_KRAN_POSITION AS z ON z.ID = f.ZielPositionID
   ORDER BY f.ID;
END;
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
      AuftragTeilfahrt int NULL,
      AuftragsTyp nvarchar(30) NULL,
      Quelle nvarchar(128) NULL,
      Ziel nvarchar(128) NULL,
      QuellePositionID bigint NULL,
      ZielPositionID bigint NULL,
      QuelleUnterposition int NULL,
      ZielUnterposition int NULL,
      SollMengeKg decimal(18,3) NULL
   );

   BEGIN TRANSACTION;

   IF EXISTS (SELECT 1 FROM dbo.FALCOM_AKTUELLE_FAHRT WITH (UPDLOCK, HOLDLOCK))
   BEGIN
      INSERT INTO @Result(Created, Reason, AktuelleFahrtID, AuftragID, AuftragTeilfahrt, AuftragsTyp, Quelle, Ziel, QuellePositionID, ZielPositionID, QuelleUnterposition, ZielUnterposition, SollMengeKg)
      SELECT TOP (1)
         CAST(0 AS bit),
         N'FALCOM_AKTUELLE_FAHRT ist bereits belegt.',
         f.ID,
         f.AuftragID,
         f.AuftragTeilfahrt,
         f.AuftragsTyp,
         q.Bezeichnung,
         z.Bezeichnung,
         f.QuellePositionID,
         f.ZielPositionID,
         f.QuelleUnterposition,
         f.ZielUnterposition,
         f.SollMengeKg
      FROM dbo.FALCOM_AKTUELLE_FAHRT AS f
      LEFT JOIN dbo.FALCOM_KRAN_POSITION AS q ON q.ID = f.QuellePositionID
      LEFT JOIN dbo.FALCOM_KRAN_POSITION AS z ON z.ID = f.ZielPositionID
      ORDER BY f.ID;

      COMMIT TRANSACTION;
      SELECT * FROM @Result;
      RETURN;
   END;

   DECLARE @AuftragTeilfahrt int = 1;
   DECLARE @NeueFahrtID table (ID bigint NOT NULL);
   DECLARE @QuellePositionID bigint;
   DECLARE @ZielPositionID bigint;
   DECLARE @QuelleUnterposition int = 0;
   DECLARE @ZielUnterposition int = 0;

   DECLARE @ChargierAuftragID bigint;
   DECLARE @ChargierwagenNr int;
   DECLARE @ZielgewichtKg decimal(18,3);
   DECLARE @QuellBoxID bigint;
   DECLARE @QuellSollMengeKg decimal(18,3);
   DECLARE @SchonProduziertKg decimal(18,3);
   DECLARE @AuftragIstGesamtKg decimal(18,3);
   DECLARE @AuftragRestKg decimal(18,3);
   DECLARE @QuellRestMengeKg decimal(18,3);
   DECLARE @FahrtSollMengeKg decimal(18,3);

   SELECT TOP (1)
      @ChargierAuftragID = a.ID,
      @ChargierwagenNr = a.ChargierwagenNr,
      @ZielgewichtKg = CONVERT(decimal(18,3), a.ZielgewichtKg)
   FROM dbo.FALCOM_AUFTRAG AS a WITH (UPDLOCK, HOLDLOCK)
   WHERE (@AuftragID IS NULL OR a.ID = @AuftragID)
     AND a.Status IN (N'FREIGEGEBEN', N'IN_ARBEIT')
     AND a.Gesperrt = 0
     AND a.FertigDatumZeit IS NULL
   ORDER BY a.EingabeDatumZeit ASC, a.ID ASC;

   IF @ChargierAuftragID IS NOT NULL
   BEGIN
      SELECT @AuftragIstGesamtKg = COALESCE(SUM(d.IstMengeKg), 0)
      FROM dbo.FALCOM_AUFTRAG_PRODUKTION AS p WITH (UPDLOCK, HOLDLOCK)
      LEFT JOIN dbo.FALCOM_AUFTRAG_PRODUKTION_DETAIL AS d ON d.ProduktionID = p.ID
      WHERE p.AuftragID = @ChargierAuftragID;

      SET @AuftragRestKg = COALESCE(@ZielgewichtKg, 0) - COALESCE(@AuftragIstGesamtKg, 0);

      IF @AuftragRestKg <= 0
      BEGIN
         UPDATE dbo.FALCOM_AUFTRAG
            SET Status = N'FERTIG',
                FertigDatumZeit = COALESCE(FertigDatumZeit, SYSDATETIME()),
                GeaendertDatumZeit = SYSDATETIME()
          WHERE ID = @ChargierAuftragID;

         INSERT INTO @Result(Created, Reason, AuftragID, AuftragTeilfahrt, AuftragsTyp, SollMengeKg)
         VALUES (CAST(0 AS bit), N'Auftrag ist durch Produktionsdetails mengenmaessig vollstaendig. Keine weitere Fahrt erzeugt.', @ChargierAuftragID, @AuftragTeilfahrt, N'CHARGIEREN', @AuftragRestKg);

         COMMIT TRANSACTION;
         SELECT * FROM @Result;
         RETURN;
      END;

      SELECT @ZielPositionID = p.ID
      FROM dbo.FALCOM_KRAN_POSITION AS p
      WHERE p.ID = @ChargierwagenNr
        AND p.Art = N'ZIEL';

      IF @ZielPositionID IS NULL
      BEGIN
         SELECT @ZielPositionID = p.ID
         FROM dbo.FALCOM_KRAN_POSITION AS p
         WHERE p.PositionsTyp = N'CHARGIERWAGEN'
           AND p.PositionsNr = @ChargierwagenNr;
      END;

      SELECT TOP (1)
         @QuellBoxID = b.BoxID,
         @QuellSollMengeKg = CONVERT(decimal(18,3), COALESCE(NULLIF(b.BerechnetKg, 0), NULLIF(CONVERT(decimal(18,3), b.Menge), 0))),
         @QuellePositionID = qp.ID,
         @SchonProduziertKg = COALESCE(prod.IstMengeKg, 0)
      FROM dbo.FALCOM_AUFTRAG_BERECHNET AS b WITH (UPDLOCK, HOLDLOCK)
      OUTER APPLY
      (
         SELECT TOP (1) p.ID
         FROM dbo.FALCOM_KRAN_POSITION AS p
         WHERE (p.PositionsTyp = N'LAGERBOX' AND p.PositionsNr = b.BoxID)
            OR (p.PositionsTyp = N'LKW_PLATZ' AND b.BoxID BETWEEN 101 AND 103 AND p.PositionsNr = b.BoxID - 100)
         ORDER BY p.ID
      ) AS qp
      OUTER APPLY
      (
         SELECT SUM(d.IstMengeKg) AS IstMengeKg
         FROM dbo.FALCOM_AUFTRAG_PRODUKTION AS p WITH (UPDLOCK, HOLDLOCK)
         LEFT JOIN dbo.FALCOM_AUFTRAG_PRODUKTION_DETAIL AS d ON d.ProduktionID = p.ID
         WHERE p.AuftragID = b.AuftragID
           AND p.QuelleKranPositionID = qp.ID
           AND p.ZielKranPositionID = @ZielPositionID
      ) AS prod
      WHERE b.AuftragID = @ChargierAuftragID
        AND b.BerechnungAktiv = 1
        AND COALESCE(NULLIF(b.BerechnetKg, 0), NULLIF(CONVERT(decimal(18,3), b.Menge), 0), 0) > 0
        AND qp.ID IS NOT NULL
        AND @ZielPositionID IS NOT NULL
        AND COALESCE(prod.IstMengeKg, 0) < CONVERT(decimal(18,3), COALESCE(NULLIF(b.BerechnetKg, 0), NULLIF(CONVERT(decimal(18,3), b.Menge), 0)))
      ORDER BY b.PositionsNr ASC, b.ID ASC;

      IF @QuellBoxID IS NULL
      BEGIN
         UPDATE dbo.FALCOM_AUFTRAG
            SET Status = N'FERTIG',
                FertigDatumZeit = COALESCE(FertigDatumZeit, SYSDATETIME()),
                GeaendertDatumZeit = SYSDATETIME()
          WHERE ID = @ChargierAuftragID
            AND EXISTS (SELECT 1 FROM dbo.FALCOM_AUFTRAG_PRODUKTION WHERE AuftragID = @ChargierAuftragID);

         INSERT INTO @Result(Created, Reason, AuftragID, AuftragTeilfahrt, AuftragsTyp)
         VALUES (CAST(0 AS bit), N'Fuer den Auftrag wurde keine offene berechnete Quelle mit Restmenge gefunden.', @ChargierAuftragID, @AuftragTeilfahrt, N'CHARGIEREN');

         COMMIT TRANSACTION;
         SELECT * FROM @Result;
         RETURN;
      END;

      IF @QuellePositionID IS NULL OR @ZielPositionID IS NULL
      BEGIN
         INSERT INTO @Result(Created, Reason, AuftragID, AuftragTeilfahrt, AuftragsTyp, SollMengeKg)
         VALUES (CAST(0 AS bit), N'Quelle oder Ziel konnte nicht auf FALCOM_KRAN_POSITION abgebildet werden.', @ChargierAuftragID, @AuftragTeilfahrt, N'CHARGIEREN', @QuellSollMengeKg);

         COMMIT TRANSACTION;
         SELECT * FROM @Result;
         RETURN;
      END;

      EXEC dbo.FALCOM_GetNextKranPositionUnterposition
         @PositionID = @QuellePositionID,
         @Rolle = N'QUELLE',
         @Unterposition = @QuelleUnterposition OUTPUT;

      EXEC dbo.FALCOM_GetNextKranPositionUnterposition
         @PositionID = @ZielPositionID,
         @Rolle = N'ZIEL',
         @Unterposition = @ZielUnterposition OUTPUT;

      SET @QuellRestMengeKg = COALESCE(@QuellSollMengeKg, 0) - COALESCE(@SchonProduziertKg, 0);
      SET @FahrtSollMengeKg = CASE
         WHEN @QuellRestMengeKg <= 0 THEN 0
         WHEN @AuftragRestKg <= 0 THEN 0
         WHEN @QuellRestMengeKg < @AuftragRestKg THEN @QuellRestMengeKg
         ELSE @AuftragRestKg
      END;

      IF @FahrtSollMengeKg <= 0
      BEGIN
         INSERT INTO @Result(Created, Reason, AuftragID, AuftragTeilfahrt, AuftragsTyp, SollMengeKg)
         VALUES (CAST(0 AS bit), N'Keine positive Restmenge fuer die naechste Chargierfahrt vorhanden.', @ChargierAuftragID, @AuftragTeilfahrt, N'CHARGIEREN', @FahrtSollMengeKg);

         COMMIT TRANSACTION;
         SELECT * FROM @Result;
         RETURN;
      END;

      SELECT @AuftragTeilfahrt = COALESCE(MAX(d.TeilfahrtNr), 0) + 1
      FROM dbo.FALCOM_AUFTRAG_PRODUKTION AS p
      LEFT JOIN dbo.FALCOM_AUFTRAG_PRODUKTION_DETAIL AS d ON d.ProduktionID = p.ID
      WHERE p.AuftragID = @ChargierAuftragID
        AND p.QuelleKranPositionID = @QuellePositionID
        AND p.ZielKranPositionID = @ZielPositionID;

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
         AuftragTeilfahrt,
         Bemerkung,
         Status,
         QuellePositionID,
         ZielPositionID,
         QuelleUnterposition,
         ZielUnterposition,
         SollMengeKg
      )
      OUTPUT inserted.ID INTO @NeueFahrtID(ID)
      VALUES
      (
         N'CHARGIEREN',
         @ChargierAuftragID,
         @AuftragTeilfahrt,
         CONCAT(N'Automatisch aus freigegebenem Chargierauftrag erzeugt. QuelleIst=', COALESCE(CONVERT(nvarchar(30), @SchonProduziertKg), N'0'), N' kg, QuelleRest=', COALESCE(CONVERT(nvarchar(30), @QuellRestMengeKg), N'0'), N' kg, AuftragIst=', COALESCE(CONVERT(nvarchar(30), @AuftragIstGesamtKg), N'0'), N' kg, AuftragRest=', COALESCE(CONVERT(nvarchar(30), @AuftragRestKg), N'0'), N' kg.'),
         N'OFFEN',
         @QuellePositionID,
         @ZielPositionID,
         @QuelleUnterposition,
         @ZielUnterposition,
         @FahrtSollMengeKg
      );

      INSERT INTO @Result(Created, Reason, AktuelleFahrtID, AuftragID, AuftragTeilfahrt, AuftragsTyp, Quelle, Ziel, QuellePositionID, ZielPositionID, QuelleUnterposition, ZielUnterposition, SollMengeKg)
      SELECT
         CAST(1 AS bit),
         N'Aktuelle Chargierfahrt erzeugt.',
         nf.ID,
         @ChargierAuftragID,
         @AuftragTeilfahrt,
         N'CHARGIEREN',
         q.Bezeichnung,
         z.Bezeichnung,
         @QuellePositionID,
         @ZielPositionID,
         @QuelleUnterposition,
         @ZielUnterposition,
         @FahrtSollMengeKg
      FROM @NeueFahrtID AS nf
      LEFT JOIN dbo.FALCOM_KRAN_POSITION AS q ON q.ID = @QuellePositionID
      LEFT JOIN dbo.FALCOM_KRAN_POSITION AS z ON z.ID = @ZielPositionID;

      COMMIT TRANSACTION;
      SELECT * FROM @Result;
      RETURN;
   END;

   IF @AuftragID IS NOT NULL
   BEGIN
      INSERT INTO @Result(Created, Reason)
      VALUES (CAST(0 AS bit), N'Kein freigegebener oder aktiver Auftrag gefunden.');

      COMMIT TRANSACTION;
      SELECT * FROM @Result;
      RETURN;
   END;

   DECLARE @EinlagerID bigint;
   DECLARE @LkwPlatzNr int;
   DECLARE @ZielBoxID bigint;
   DECLARE @EinlagerErwartet decimal(18,3);

   SELECT TOP (1)
      @EinlagerID = e.ID,
      @LkwPlatzNr = e.LkwPlatzNr,
      @ZielBoxID = e.ZielBoxID,
      @EinlagerErwartet = e.ErwarteteMengeKg
   FROM dbo.FALCOM_EINLAGER_AUFTRAG AS e WITH (UPDLOCK, HOLDLOCK)
   WHERE e.DatumZeitFertig IS NULL
   ORDER BY e.Reihenfolge ASC, e.ID ASC;

   IF @EinlagerID IS NOT NULL
   BEGIN
      SET @QuelleUnterposition = 0;
      SET @ZielUnterposition = 0;

      SELECT @QuellePositionID = p.ID
      FROM dbo.FALCOM_KRAN_POSITION AS p
      WHERE p.PositionsTyp = N'LKW_PLATZ'
        AND p.PositionsNr = @LkwPlatzNr;

      SELECT @ZielPositionID = p.ID
      FROM dbo.FALCOM_KRAN_POSITION AS p
      WHERE p.ID = @ZielBoxID;

      SELECT @AuftragTeilfahrt = COALESCE(MAX(d.HubNr), 0) + 1
      FROM dbo.FALCOM_EINLAGER_AUFTRAG_PRODUKTION_DETAIL AS d WITH (UPDLOCK, HOLDLOCK)
      WHERE d.LagerAuftragID = @EinlagerID;

      IF @QuellePositionID IS NULL OR @ZielPositionID IS NULL
      BEGIN
         INSERT INTO @Result(Created, Reason, AuftragID, AuftragTeilfahrt, AuftragsTyp, SollMengeKg)
         VALUES (CAST(0 AS bit), N'Quelle oder Ziel des Einlagerauftrags konnte nicht auf FALCOM_KRAN_POSITION abgebildet werden.', @EinlagerID, @AuftragTeilfahrt, N'EINLAGERN', CAST(0 AS decimal(18,3)));

         COMMIT TRANSACTION;
         SELECT * FROM @Result;
         RETURN;
      END;

      EXEC dbo.FALCOM_GetNextKranPositionUnterposition
         @PositionID = @QuellePositionID,
         @Rolle = N'QUELLE',
         @Unterposition = @QuelleUnterposition OUTPUT;

      EXEC dbo.FALCOM_GetNextKranPositionUnterposition
         @PositionID = @ZielPositionID,
         @Rolle = N'ZIEL',
         @Unterposition = @ZielUnterposition OUTPUT;

      INSERT INTO dbo.FALCOM_AKTUELLE_FAHRT
      (
         AuftragsTyp,
         AuftragID,
         AuftragTeilfahrt,
         Bemerkung,
         Status,
         QuellePositionID,
         ZielPositionID,
         QuelleUnterposition,
         ZielUnterposition,
         SollMengeKg
      )
      OUTPUT inserted.ID INTO @NeueFahrtID(ID)
      VALUES
      (
         N'EINLAGERN',
         @EinlagerID,
         @AuftragTeilfahrt,
         N'Automatisch aus offenem Einlagerauftrag erzeugt. Einlagerfahrt hat keine Sollmenge; gefahren wird bis LKW leer meldet.',
         N'OFFEN',
         @QuellePositionID,
         @ZielPositionID,
         @QuelleUnterposition,
         @ZielUnterposition,
         CAST(0 AS decimal(18,3))
      );

      INSERT INTO @Result(Created, Reason, AktuelleFahrtID, AuftragID, AuftragTeilfahrt, AuftragsTyp, Quelle, Ziel, QuellePositionID, ZielPositionID, QuelleUnterposition, ZielUnterposition, SollMengeKg)
      SELECT
         CAST(1 AS bit),
         N'Aktuelle Einlagerfahrt erzeugt.',
         nf.ID,
         @EinlagerID,
         @AuftragTeilfahrt,
         N'EINLAGERN',
         q.Bezeichnung,
         z.Bezeichnung,
         @QuellePositionID,
         @ZielPositionID,
         @QuelleUnterposition,
         @ZielUnterposition,
         CAST(0 AS decimal(18,3))
      FROM @NeueFahrtID AS nf
      LEFT JOIN dbo.FALCOM_KRAN_POSITION AS q ON q.ID = @QuellePositionID
      LEFT JOIN dbo.FALCOM_KRAN_POSITION AS z ON z.ID = @ZielPositionID;

      COMMIT TRANSACTION;
      SELECT * FROM @Result;
      RETURN;
   END;

   INSERT INTO @Result(Created, Reason)
   VALUES (CAST(0 AS bit), N'Keine Kranfahrt offen.');

   COMMIT TRANSACTION;
   SELECT * FROM @Result;
END;
GO
