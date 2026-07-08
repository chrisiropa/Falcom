USE [FG]
GO

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

UPDATE dbo.FALCOM_KRAN_POSITION
   SET X_mm = 1000,
       Y_mm = 0,
       Breite_mm = 4000,
       Laenge_mm = 7000,
       AnfahrX_mm = 3000,
       AnfahrY_mm = 3500,
       AnfahrZ_mm = 8500,
       Bemerkung = N'Senkrecht vor Reihe 1, mittig zur 6000-mm-Boxreihe'
WHERE PositionsTyp = N'CHARGIERWAGEN'
  AND PositionsNr = 1;

UPDATE dbo.FALCOM_KRAN_POSITION
   SET X_mm = 7000,
       Y_mm = 0,
       Breite_mm = 4000,
       Laenge_mm = 7000,
       AnfahrX_mm = 9000,
       AnfahrY_mm = 3500,
       AnfahrZ_mm = 8500,
       Bemerkung = N'Senkrecht vor Reihe 2, mittig zur 6000-mm-Boxreihe'
WHERE PositionsTyp = N'CHARGIERWAGEN'
  AND PositionsNr = 2;

UPDATE dbo.FALCOM_KRAN_POSITION
   SET X_mm = 13000,
       Y_mm = 0,
       Breite_mm = 4000,
       Laenge_mm = 7000,
       AnfahrX_mm = 15000,
       AnfahrY_mm = 3500,
       AnfahrZ_mm = 8500,
       Bemerkung = N'Senkrecht vor Reihe 3, mittig zur 6000-mm-Boxreihe'
WHERE PositionsTyp = N'CHARGIERWAGEN'
  AND PositionsNr = 3;

COMMIT TRANSACTION;
GO
