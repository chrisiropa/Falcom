SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_CompleteEinlagerAuftragLkwLeer
   @AuftragsNummer bigint,
   @AuftragTeilfahrt int,
   @LkwPlatzPositionID int,
   @AenderungsZaehler int
AS
BEGIN
   SET NOCOUNT ON;
   SET XACT_ABORT ON;

   DECLARE @Jetzt datetime2(3) = SYSDATETIME();
   DECLARE @DbLkwPlatzNr int;
   DECLARE @DbLkwPlatzPositionID int;
   DECLARE @FertigDatumZeit datetime2(3);
   DECLARE @IstMengeKg decimal(18,3);

   BEGIN TRANSACTION;

   SELECT
      @DbLkwPlatzNr = e.LkwPlatzNr,
      @FertigDatumZeit = CONVERT(datetime2(3), e.DatumZeitFertig),
      @IstMengeKg = e.IstMengeKg
   FROM dbo.FALCOM_EINLAGER_AUFTRAG AS e WITH (UPDLOCK, HOLDLOCK)
   WHERE e.ID = @AuftragsNummer;

   SELECT @DbLkwPlatzPositionID = CONVERT(int, p.ID)
   FROM dbo.FALCOM_KRAN_POSITION AS p
   WHERE p.PositionsTyp = N'LKW_PLATZ'
     AND p.PositionsNr = @DbLkwPlatzNr;

   IF @DbLkwPlatzNr IS NULL
   BEGIN
      COMMIT TRANSACTION;
      SELECT
         CAST(0 AS bit) AS Completed,
         N'Event_207 verweist auf keinen vorhandenen Einlagerauftrag.' AS Reason,
         @AuftragsNummer AS AuftragID,
         @AuftragTeilfahrt AS AuftragTeilfahrt,
         N'EINLAGERN' AS AuftragsTyp;
      RETURN;
   END;

   IF @FertigDatumZeit IS NOT NULL
   BEGIN
      COMMIT TRANSACTION;
      SELECT
         CAST(0 AS bit) AS Completed,
         N'Einlagerauftrag war bereits abgeschlossen; der Event_207-Initialwert wurde ohne Zustandsaenderung ignoriert.' AS Reason,
         @AuftragsNummer AS AuftragID,
         @AuftragTeilfahrt AS AuftragTeilfahrt,
         N'EINLAGERN_BEREITS_FERTIG' AS AuftragsTyp,
         @IstMengeKg AS IstMengeKg;
      RETURN;
   END;

   IF @DbLkwPlatzPositionID IS NULL
      OR @DbLkwPlatzPositionID <> @LkwPlatzPositionID
   BEGIN
      COMMIT TRANSACTION;
      SELECT
         CAST(0 AS bit) AS Completed,
         CONCAT(N'Event_207 LKW-Kranposition passt nicht zum Einlagerauftrag. ErwartetPositionID=', COALESCE(CONVERT(nvarchar(20), @DbLkwPlatzPositionID), N'<nicht gefunden>'), N', empfangenPositionID=', @LkwPlatzPositionID, N'.') AS Reason,
         @AuftragsNummer AS AuftragID,
         @AuftragTeilfahrt AS AuftragTeilfahrt,
         N'EINLAGERN' AS AuftragsTyp,
         @IstMengeKg AS IstMengeKg;
      RETURN;
   END;

   UPDATE dbo.FALCOM_EINLAGER_AUFTRAG
      SET DatumZeitFertig = COALESCE(DatumZeitFertig, @Jetzt)
    WHERE ID = @AuftragsNummer;

   UPDATE dbo.FALCOM_EINLAGER_AUFTRAG_PRODUKTION
      SET Status = N'FERTIG',
          FertigDatumZeit = COALESCE(FertigDatumZeit, @Jetzt),
          GrundFahrtende = N'REGULAER'
    WHERE EinlagerAuftragID = @AuftragsNummer;

   COMMIT TRANSACTION;

   SELECT
      CAST(1 AS bit) AS Completed,
      CONCAT(N'LKW-Platz mit Kranposition ', @LkwPlatzPositionID, N' wurde durch Event_207 leer gemeldet. Einlagerauftrag regulaer beendet. Trigger=', @AenderungsZaehler, N'.') AS Reason,
      @AuftragsNummer AS AuftragID,
      @AuftragTeilfahrt AS AuftragTeilfahrt,
      N'EINLAGERN' AS AuftragsTyp,
      @IstMengeKg AS IstMengeKg;
END;
GO
