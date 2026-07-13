USE [FG]
GO

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

UPDATE dbo.FALCOM_KRAN_POSITION
   SET Start_KatzeY = 4000,
       Breite_mm = 6000,
       Laenge_mm = 3200,
       AbwurfPosKatzeX = 15000,
       AbwurfPosKranX = 5600,
       AnfahrZ_mm = 8500,
       Bemerkung = N'Reihe 3, 5 gleich hohe Boxen'
WHERE PositionsTyp = N'LAGERBOX'
  AND PositionsNr = 13;

UPDATE dbo.FALCOM_KRAN_POSITION
   SET Start_KatzeY = 7200,
       Breite_mm = 6000,
       Laenge_mm = 3200,
       AbwurfPosKatzeX = 15000,
       AbwurfPosKranX = 8800,
       AnfahrZ_mm = 8500,
       Bemerkung = N'Reihe 3, 5 gleich hohe Boxen'
WHERE PositionsTyp = N'LAGERBOX'
  AND PositionsNr = 12;

UPDATE dbo.FALCOM_KRAN_POSITION
   SET Start_KatzeY = 10400,
       Breite_mm = 6000,
       Laenge_mm = 3200,
       AbwurfPosKatzeX = 15000,
       AbwurfPosKranX = 12000,
       AnfahrZ_mm = 8500,
       Bemerkung = N'Reihe 3, 5 gleich hohe Boxen'
WHERE PositionsTyp = N'LAGERBOX'
  AND PositionsNr = 9;

UPDATE dbo.FALCOM_KRAN_POSITION
   SET Start_KatzeY = 13600,
       Breite_mm = 6000,
       Laenge_mm = 3200,
       AbwurfPosKatzeX = 15000,
       AbwurfPosKranX = 15200,
       AnfahrZ_mm = 8500,
       Bemerkung = N'Reihe 3, 5 gleich hohe Boxen'
WHERE PositionsTyp = N'LAGERBOX'
  AND PositionsNr = 6;

UPDATE dbo.FALCOM_KRAN_POSITION
   SET Start_KatzeY = 16800,
       Breite_mm = 6000,
       Laenge_mm = 3200,
       AbwurfPosKatzeX = 15000,
       AbwurfPosKranX = 18400,
       AnfahrZ_mm = 8500,
       Bemerkung = N'Reihe 3, 5 gleich hohe Boxen'
WHERE PositionsTyp = N'LAGERBOX'
  AND PositionsNr = 3;

COMMIT TRANSACTION;
GO
