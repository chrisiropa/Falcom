USE [FG]
GO

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

UPDATE dbo.FALCOM_KRAN_POSITION
   SET Start_KatzeX = 1000,
       Start_KatzeY = 0,
       Breite_mm = 4000,
       Laenge_mm = 7000,
       AbwurfPosKatzeX = 3000,
       AbwurfPosKranX = 3500,
       AnfahrZ_mm = 8500,
       Bemerkung = N'Senkrecht vor Reihe 1, mittig zur 6000-mm-Boxreihe'
WHERE PositionsTyp = N'CHARGIERWAGEN'
  AND PositionsNr = 1;

UPDATE dbo.FALCOM_KRAN_POSITION
   SET Start_KatzeX = 7000,
       Start_KatzeY = 0,
       Breite_mm = 4000,
       Laenge_mm = 7000,
       AbwurfPosKatzeX = 9000,
       AbwurfPosKranX = 3500,
       AnfahrZ_mm = 8500,
       Bemerkung = N'Senkrecht vor Reihe 2, mittig zur 6000-mm-Boxreihe'
WHERE PositionsTyp = N'CHARGIERWAGEN'
  AND PositionsNr = 2;

UPDATE dbo.FALCOM_KRAN_POSITION
   SET Start_KatzeX = 13000,
       Start_KatzeY = 0,
       Breite_mm = 4000,
       Laenge_mm = 7000,
       AbwurfPosKatzeX = 15000,
       AbwurfPosKranX = 3500,
       AnfahrZ_mm = 8500,
       Bemerkung = N'Senkrecht vor Reihe 3, mittig zur 6000-mm-Boxreihe'
WHERE PositionsTyp = N'CHARGIERWAGEN'
  AND PositionsNr = 3;

COMMIT TRANSACTION;
GO
