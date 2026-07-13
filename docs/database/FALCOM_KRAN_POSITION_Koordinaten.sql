USE [FG]
GO

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.FALCOM_KRAN_LAYOUT', N'U') IS NULL
BEGIN
   CREATE TABLE dbo.FALCOM_KRAN_LAYOUT
   (
      ID bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_FALCOM_KRAN_LAYOUT PRIMARY KEY,
      Name nvarchar(128) NOT NULL,
      Breite_mm int NOT NULL,
      Laenge_mm int NOT NULL,
      StartKranY int NULL,
      StopKranX int NULL,
      StartKatzeX int NULL,
      StopKatzeX int NULL,
      PosHubZ_Oben_mm int NULL,
      PosHubZ_Unten_mm int NULL,
      Aktiv bit NOT NULL CONSTRAINT DF_FALCOM_KRAN_LAYOUT_Aktiv DEFAULT 1,
      Bemerkung nvarchar(1024) NULL
   );
END;

IF COL_LENGTH(N'dbo.FALCOM_KRAN_POSITION', N'Start_KatzeX') IS NULL ALTER TABLE dbo.FALCOM_KRAN_POSITION ADD Start_KatzeX int NULL;
IF COL_LENGTH(N'dbo.FALCOM_KRAN_POSITION', N'Start_KatzeY') IS NULL ALTER TABLE dbo.FALCOM_KRAN_POSITION ADD Start_KatzeY int NULL;
IF COL_LENGTH(N'dbo.FALCOM_KRAN_POSITION', N'Breite_mm') IS NULL ALTER TABLE dbo.FALCOM_KRAN_POSITION ADD Breite_mm int NULL;
IF COL_LENGTH(N'dbo.FALCOM_KRAN_POSITION', N'Laenge_mm') IS NULL ALTER TABLE dbo.FALCOM_KRAN_POSITION ADD Laenge_mm int NULL;
IF COL_LENGTH(N'dbo.FALCOM_KRAN_POSITION', N'AbwurfPosKatzeX') IS NULL ALTER TABLE dbo.FALCOM_KRAN_POSITION ADD AbwurfPosKatzeX int NULL;
IF COL_LENGTH(N'dbo.FALCOM_KRAN_POSITION', N'AbwurfPosKranX') IS NULL ALTER TABLE dbo.FALCOM_KRAN_POSITION ADD AbwurfPosKranX int NULL;
IF COL_LENGTH(N'dbo.FALCOM_KRAN_POSITION', N'AnfahrZ_mm') IS NULL ALTER TABLE dbo.FALCOM_KRAN_POSITION ADD AnfahrZ_mm int NULL;

IF NOT EXISTS (SELECT 1 FROM dbo.FALCOM_KRAN_LAYOUT WHERE Name = N'Standard 24x24m')
BEGIN
   INSERT dbo.FALCOM_KRAN_LAYOUT
   (Name, Breite_mm, Laenge_mm, StartKranY, StopKranX, StartKatzeX, StopKatzeX, PosHubZ_Oben_mm, PosHubZ_Unten_mm, Aktiv, Bemerkung)
   VALUES
   (N'Standard 24x24m', 24000, 24000, 1000, 24000, 500, 23500, 200, 8500, 1, N'Standardlayout: CW oben, Lagerboxen mittig, LKW-Abladepl‰tze unten.');
END;
ELSE
BEGIN
   UPDATE dbo.FALCOM_KRAN_LAYOUT
      SET Breite_mm = 24000,
          Laenge_mm = 24000,
          StartKranY = 1000,
          StopKranX = 24000,
          StartKatzeX = 500,
          StopKatzeX = 23500,
          PosHubZ_Oben_mm = 200,
          PosHubZ_Unten_mm = 8500,
          Aktiv = 1,
          Bemerkung = N'Standardlayout: CW oben, Lagerboxen mittig, LKW-Abladepl‰tze unten.'
   WHERE Name = N'Standard 24x24m';
END;

DECLARE @untenZ int = 8500;

DECLARE @positionen TABLE
(
   ID bigint NOT NULL,
   PositionsTyp nvarchar(64) NOT NULL,
   PositionsNr int NOT NULL,
   Bezeichnung nvarchar(128) NOT NULL,
   Art nvarchar(64) NOT NULL,
   Bemerkung nvarchar(512) NOT NULL,
   Start_KatzeX int NOT NULL,
   Start_KatzeY int NOT NULL,
   Breite_mm int NOT NULL,
   Laenge_mm int NOT NULL
);

INSERT @positionen VALUES
(34, N'CHARGIERWAGEN', 101, N'Chargierwagen 1 Maul',   N'ZIEL', N'Abladeposition Maul',   1000, 0,    4000, 1333),
(31, N'CHARGIERWAGEN', 1,   N'Chargierwagen 1 Mitte',  N'ZIEL', N'Abladeposition Mitte',  1000, 1333, 4000, 1334),
(35, N'CHARGIERWAGEN', 103, N'Chargierwagen 1 Hinten', N'ZIEL', N'Abladeposition Hinten', 1000, 2667, 4000, 1333),
(36, N'CHARGIERWAGEN', 201, N'Chargierwagen 2 Maul',   N'ZIEL', N'Abladeposition Maul',   7000, 0,    4000, 1333),
(32, N'CHARGIERWAGEN', 2,   N'Chargierwagen 2 Mitte',  N'ZIEL', N'Abladeposition Mitte',  7000, 1333, 4000, 1334),
(37, N'CHARGIERWAGEN', 203, N'Chargierwagen 2 Hinten', N'ZIEL', N'Abladeposition Hinten', 7000, 2667, 4000, 1333),
(38, N'CHARGIERWAGEN', 301, N'Chargierwagen 3 Maul',   N'ZIEL', N'Abladeposition Maul',   13000, 0,    4000, 1333),
(33, N'CHARGIERWAGEN', 3,   N'Chargierwagen 3 Mitte',  N'ZIEL', N'Abladeposition Mitte',  13000, 1333, 4000, 1334),
(39, N'CHARGIERWAGEN', 303, N'Chargierwagen 3 Hinten', N'ZIEL', N'Abladeposition Hinten', 13000, 2667, 4000, 1333),

(1,  N'LAGERBOX', 1,  N'Lagerbox 1',  N'QUELLE_UND_ZIEL', N'Reihe 1 unten',      0,     17334, 6000, 2666),
(4,  N'LAGERBOX', 4,  N'Lagerbox 4',  N'QUELLE_UND_ZIEL', N'Reihe 1 unten',      0,     14667, 6000, 2667),
(7,  N'LAGERBOX', 7,  N'Lagerbox 7',  N'QUELLE_UND_ZIEL', N'Reihe 1 oben groﬂ',  0,      9334, 6000, 5333),
(10, N'LAGERBOX', 10, N'Lagerbox 10', N'QUELLE_UND_ZIEL', N'Reihe 1 oben groﬂ',  0,      4000, 6000, 5334),

(2,  N'LAGERBOX', 2,  N'Lagerbox 2',  N'QUELLE_UND_ZIEL', N'Reihe 2', 6000, 16000, 6000, 4000),
(5,  N'LAGERBOX', 5,  N'Lagerbox 5',  N'QUELLE_UND_ZIEL', N'Reihe 2', 6000, 12000, 6000, 4000),
(8,  N'LAGERBOX', 8,  N'Lagerbox 8',  N'QUELLE_UND_ZIEL', N'Reihe 2', 6000,  8000, 6000, 4000),
(11, N'LAGERBOX', 11, N'Lagerbox 11', N'QUELLE_UND_ZIEL', N'Reihe 2', 6000,  4000, 6000, 4000),

(3,  N'LAGERBOX', 3,  N'Lagerbox 3',  N'QUELLE_UND_ZIEL', N'Reihe 3', 12000, 16800, 6000, 3200),
(6,  N'LAGERBOX', 6,  N'Lagerbox 6',  N'QUELLE_UND_ZIEL', N'Reihe 3', 12000, 13600, 6000, 3200),
(9,  N'LAGERBOX', 9,  N'Lagerbox 9',  N'QUELLE_UND_ZIEL', N'Reihe 3', 12000, 10400, 6000, 3200),
(12, N'LAGERBOX', 12, N'Lagerbox 12', N'QUELLE_UND_ZIEL', N'Reihe 3', 12000,  7200, 6000, 3200),
(13, N'LAGERBOX', 13, N'Lagerbox 13', N'QUELLE_UND_ZIEL', N'Reihe 3', 12000,  4000, 6000, 3200),

(21, N'LKW_PLATZ', 1, N'LKW Abladeplatz 1', N'QUELLE', N'LKW-Abladeplatz',     0, 20000, 6000, 4000),
(22, N'LKW_PLATZ', 2, N'LKW Abladeplatz 2', N'QUELLE', N'LKW-Abladeplatz',  6000, 20000, 6000, 4000),
(23, N'LKW_PLATZ', 3, N'LKW Abladeplatz 3', N'QUELLE', N'LKW-Abladeplatz', 12000, 20000, 6000, 4000);

MERGE dbo.FALCOM_KRAN_POSITION AS target
USING @positionen AS source
   ON target.PositionsTyp = source.PositionsTyp
  AND target.PositionsNr = source.PositionsNr
WHEN MATCHED THEN
   UPDATE SET
      Bezeichnung = source.Bezeichnung,
      Art = source.Art,
      Bemerkung = source.Bemerkung,
      Start_KatzeX = source.Start_KatzeX,
      Start_KatzeY = source.Start_KatzeY,
      Breite_mm = source.Breite_mm,
      Laenge_mm = source.Laenge_mm,
      AbwurfPosKatzeX = source.Start_KatzeX + source.Breite_mm / 2,
      AbwurfPosKranX = source.Start_KatzeY + source.Laenge_mm / 2,
      AnfahrZ_mm = @untenZ
WHEN NOT MATCHED THEN
   INSERT (ID, PositionsTyp, PositionsNr, Bezeichnung, Art, Bemerkung, Start_KatzeX, Start_KatzeY, Breite_mm, Laenge_mm, AbwurfPosKatzeX, AbwurfPosKranX, AnfahrZ_mm)
   VALUES (source.ID, source.PositionsTyp, source.PositionsNr, source.Bezeichnung, source.Art, source.Bemerkung, source.Start_KatzeX, source.Start_KatzeY, source.Breite_mm, source.Laenge_mm, source.Start_KatzeX + source.Breite_mm / 2, source.Start_KatzeY + source.Laenge_mm / 2, @untenZ);

COMMIT TRANSACTION;
GO
