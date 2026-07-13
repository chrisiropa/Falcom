USE [FG]
GO

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

-- Mittlere Reihe an linke Reihe angleichen:
-- Box 8 wie Box 7, Box 5 wie Box 4, Box 2 wie Box 1.
UPDATE dbo.FALCOM_KRAN_POSITION
   SET Start_KatzeY = 9334,
       Breite_mm = 6000,
       Laenge_mm = 5333,
       AbwurfPosKatzeX = 9000,
       AbwurfPosKranX = 12000,
       AnfahrZ_mm = 8500,
       Bemerkung = N'Reihe 2 oben groﬂ, wie Box 7'
WHERE PositionsTyp = N'LAGERBOX'
  AND PositionsNr = 8;

UPDATE dbo.FALCOM_KRAN_POSITION
   SET Start_KatzeY = 14667,
       Breite_mm = 6000,
       Laenge_mm = 2667,
       AbwurfPosKatzeX = 9000,
       AbwurfPosKranX = 16000,
       AnfahrZ_mm = 8500,
       Bemerkung = N'Reihe 2 unten, wie Box 4'
WHERE PositionsTyp = N'LAGERBOX'
  AND PositionsNr = 5;

UPDATE dbo.FALCOM_KRAN_POSITION
   SET Start_KatzeY = 17334,
       Breite_mm = 6000,
       Laenge_mm = 2666,
       AbwurfPosKatzeX = 9000,
       AbwurfPosKranX = 18667,
       AnfahrZ_mm = 8500,
       Bemerkung = N'Reihe 2 unten, wie Box 1'
WHERE PositionsTyp = N'LAGERBOX'
  AND PositionsNr = 2;

-- Chargierwagen senkrecht vor die drei Reihen stellen:
-- Breite/Laenge getauscht, innerhalb jeder 6000-mm-Reihe mittig platziert.
UPDATE dbo.FALCOM_KRAN_POSITION
   SET Start_KatzeX = 2333,
       Start_KatzeY = 0,
       Breite_mm = 1334,
       Laenge_mm = 4000,
       AbwurfPosKatzeX = 3000,
       AbwurfPosKranX = 2000,
       AnfahrZ_mm = 8500,
       Bezeichnung = N'Chargierwagen 1',
       Bemerkung = N'Senkrecht vor Reihe 1'
WHERE PositionsTyp = N'CHARGIERWAGEN'
  AND PositionsNr = 1;

UPDATE dbo.FALCOM_KRAN_POSITION
   SET Start_KatzeX = 8333,
       Start_KatzeY = 0,
       Breite_mm = 1334,
       Laenge_mm = 4000,
       AbwurfPosKatzeX = 9000,
       AbwurfPosKranX = 2000,
       AnfahrZ_mm = 8500,
       Bezeichnung = N'Chargierwagen 2',
       Bemerkung = N'Senkrecht vor Reihe 2'
WHERE PositionsTyp = N'CHARGIERWAGEN'
  AND PositionsNr = 2;

UPDATE dbo.FALCOM_KRAN_POSITION
   SET Start_KatzeX = 14333,
       Start_KatzeY = 0,
       Breite_mm = 1334,
       Laenge_mm = 4000,
       AbwurfPosKatzeX = 15000,
       AbwurfPosKranX = 2000,
       AnfahrZ_mm = 8500,
       Bezeichnung = N'Chargierwagen 3',
       Bemerkung = N'Senkrecht vor Reihe 3'
WHERE PositionsTyp = N'CHARGIERWAGEN'
  AND PositionsNr = 3;

COMMIT TRANSACTION;
GO
