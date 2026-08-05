SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER FUNCTION dbo.FALCOM_GetKranPositionAnfahrpunkte()
RETURNS @Anfahrpunkte TABLE
(
   PositionID bigint NOT NULL,
   PositionsTyp nvarchar(128) NULL,
   PositionsNr int NULL,
   Bezeichnung nvarchar(256) NULL,
   PunktNr int NOT NULL,
   KatzeX int NOT NULL,
   KranY int NOT NULL
)
AS
BEGIN
   ;
   WITH
   LayoutAktiv AS
   (
      SELECT TOP (1)
         MagnetLaenge = CONVERT(float, COALESCE(MagnetLaenge, 0)),
         MagnetBreite = CONVERT(float, COALESCE(MagnetBreite, 0)),
         RandAbstand = CONVERT(float, COALESCE(RandAbstand, 0))
      FROM dbo.FALCOM_KRAN_LAYOUT
      WHERE Aktiv = 1
      ORDER BY ID
   ),
   Zahlen AS
   (
      SELECT TOP (10000)
         ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS N
      FROM sys.all_objects AS a
      CROSS JOIN sys.all_objects AS b
   ),
   Basis AS
   (
      SELECT
         PositionID = p.ID,
         p.PositionsTyp,
         p.PositionsNr,
         p.Bezeichnung,
         PunktAnzahl =
            CASE
               WHEN p.AnzahlPositionen > 0 THEN p.AnzahlPositionen
               ELSE 0
            END,
         StartX = CONVERT(float, COALESCE(p.Start_KatzeX, 0)),
         StartY = CONVERT(float, COALESCE(p.Start_KranY, 0)),
         Breite = CONVERT(float, COALESCE(p.Breite, 0)),
         Laenge = CONVERT(float, COALESCE(p.Laenge, 0)),
         layout.MagnetLaenge,
         layout.MagnetBreite,
         layout.RandAbstand
      FROM dbo.FALCOM_KRAN_POSITION AS p
      CROSS JOIN LayoutAktiv AS layout
      WHERE UPPER(LTRIM(RTRIM(COALESCE(p.PositionsTyp, N'')))) IN
         (N'LAGERBOX', N'LKW_PLATZ')
   ),
   Virtuell AS
   (
      SELECT
         b.*,
         VirtuellMinX =
            CASE
               WHEN b.StartX + b.RandAbstand + (b.MagnetBreite / 2.0)
                    <= b.StartX + b.Breite - b.RandAbstand - (b.MagnetBreite / 2.0)
                  THEN b.StartX + b.RandAbstand + (b.MagnetBreite / 2.0)
               ELSE b.StartX + (b.Breite / 2.0)
            END,
         VirtuellMaxX =
            CASE
               WHEN b.StartX + b.RandAbstand + (b.MagnetBreite / 2.0)
                    <= b.StartX + b.Breite - b.RandAbstand - (b.MagnetBreite / 2.0)
                  THEN b.StartX + b.Breite - b.RandAbstand - (b.MagnetBreite / 2.0)
               ELSE b.StartX + (b.Breite / 2.0)
            END,
         VirtuellMinY =
            CASE
               WHEN b.StartY + b.RandAbstand + (b.MagnetLaenge / 2.0)
                    <= b.StartY + b.Laenge - b.RandAbstand - (b.MagnetLaenge / 2.0)
                  THEN b.StartY + b.RandAbstand + (b.MagnetLaenge / 2.0)
               ELSE b.StartY + (b.Laenge / 2.0)
            END,
         VirtuellMaxY =
            CASE
               WHEN b.StartY + b.RandAbstand + (b.MagnetLaenge / 2.0)
                    <= b.StartY + b.Laenge - b.RandAbstand - (b.MagnetLaenge / 2.0)
                  THEN b.StartY + b.Laenge - b.RandAbstand - (b.MagnetLaenge / 2.0)
               ELSE b.StartY + (b.Laenge / 2.0)
            END
      FROM Basis AS b
   ),
   Zeilenwahl AS
   (
      SELECT
         v.*,
         Reihen = w.Reihen
      FROM Virtuell AS v
      CROSS APPLY
      (
         SELECT TOP (1)
            Reihen = z.N
         FROM Zahlen AS z
         CROSS APPLY
         (
            SELECT Spalten = CONVERT(int, CEILING(v.PunktAnzahl * 1.0 / z.N))
         ) AS c
         WHERE z.N BETWEEN 1 AND v.PunktAnzahl
         ORDER BY
            ABS(
               (c.Spalten * 1.0 / z.N)
               - CASE
                    WHEN (v.VirtuellMaxY - v.VirtuellMinY) = 0 THEN 1.0
                    ELSE NULLIF(v.VirtuellMaxX - v.VirtuellMinX, 0) / NULLIF(v.VirtuellMaxY - v.VirtuellMinY, 0)
                 END
            ),
            (z.N * c.Spalten) - v.PunktAnzahl,
            z.N
      ) AS w
      WHERE v.PunktAnzahl > 0
        AND UPPER(LTRIM(RTRIM(COALESCE(v.PositionsTyp, N'')))) IN (N'LAGERBOX', N'LKW_PLATZ')
   ),
   Reihen AS
   (
      SELECT
         z.PositionID,
         z.PositionsTyp,
         z.PositionsNr,
         z.Bezeichnung,
         z.PunktAnzahl,
         z.Reihen,
         ReihenNr = n.N,
         VonPunkt = CONVERT(int, FLOOR(((n.N - 1) * z.PunktAnzahl * 1.0) / z.Reihen) + 1),
         BisPunkt = CONVERT(int, FLOOR((n.N * z.PunktAnzahl * 1.0) / z.Reihen)),
         z.VirtuellMinX,
         z.VirtuellMaxX,
         z.VirtuellMinY,
         z.VirtuellMaxY
      FROM Zeilenwahl AS z
      INNER JOIN Zahlen AS n
         ON n.N BETWEEN 1 AND z.Reihen
   ),
   RasterPunkte AS
   (
      SELECT
         r.PositionID,
         r.PositionsTyp,
         r.PositionsNr,
         r.Bezeichnung,
         PunktNr = p.N,
         X =
            CASE
               WHEN r.BisPunkt - r.VonPunkt = 0
                  THEN (r.VirtuellMinX + r.VirtuellMaxX) / 2.0
               ELSE r.VirtuellMinX
                    + ((r.VirtuellMaxX - r.VirtuellMinX)
                       * ((p.N - r.VonPunkt) * 1.0 / (r.BisPunkt - r.VonPunkt)))
            END,
         Y =
            CASE
               WHEN r.Reihen = 1
                  THEN (r.VirtuellMinY + r.VirtuellMaxY) / 2.0
               ELSE r.VirtuellMinY
                    + ((r.VirtuellMaxY - r.VirtuellMinY)
                       * ((r.ReihenNr - 1) * 1.0 / (r.Reihen - 1)))
            END
      FROM Reihen AS r
      INNER JOIN Zahlen AS p
         ON p.N BETWEEN r.VonPunkt AND r.BisPunkt
   )
   INSERT INTO @Anfahrpunkte
      (PositionID, PositionsTyp, PositionsNr, Bezeichnung, PunktNr, KatzeX, KranY)
   SELECT
      PositionID,
      PositionsTyp,
      PositionsNr,
      Bezeichnung,
      PunktNr,
      KatzeX = CONVERT(int, ROUND(X, 0)),
      KranY = CONVERT(int, ROUND(Y, 0))
   FROM RasterPunkte

   RETURN;
END;
GO

-- Beispiel:
-- SELECT *
-- FROM dbo.FALCOM_GetKranPositionAnfahrpunkte()
-- ORDER BY PositionID, PunktNr;
