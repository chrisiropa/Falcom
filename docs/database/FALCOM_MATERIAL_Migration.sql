USE [FG]
GO

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.FALCOM_MATERIAL', N'U') IS NULL
BEGIN
   CREATE TABLE dbo.FALCOM_MATERIAL
   (
      ID bigint IDENTITY(1,1) NOT NULL
         CONSTRAINT PK_FALCOM_MATERIAL PRIMARY KEY,
      MaterialName nvarchar(128) NOT NULL,
      Cu decimal(10,3) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_Cu DEFAULT 0,
      Mn decimal(10,3) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_Mn DEFAULT 0,
      C decimal(10,3) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_C DEFAULT 0,
      Si decimal(10,3) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_Si DEFAULT 0,
      Cr decimal(10,3) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_Cr DEFAULT 0,
      Mg decimal(10,3) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_Mg DEFAULT 0,
      Cu_Min decimal(10,3) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_CuMin DEFAULT 0,
      Cu_Max decimal(10,3) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_CuMax DEFAULT 0,
      Mn_Min decimal(10,3) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_MnMin DEFAULT 0,
      Mn_Max decimal(10,3) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_MnMax DEFAULT 0,
      Toleranz decimal(12,3) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_Toleranz DEFAULT 150,
      Beschaffenheit nvarchar(128) NOT NULL
         CONSTRAINT DF_FALCOM_MATERIAL_Beschaffenheit DEFAULT N'todo',
      Bemerkung nvarchar(1024) NULL
   );

   CREATE UNIQUE INDEX UX_FALCOM_MATERIAL_MaterialName
      ON dbo.FALCOM_MATERIAL(MaterialName);
END;

MERGE dbo.FALCOM_MATERIAL AS target
USING
(
   VALUES
      (N'GJS400',    0.050, 0.250, 3.550, 2.100, 0.050, 0.040, 0.030, 0.070, 0.230, 0.270, 150.000, N'todo', N'Vorläufige Analyse, bitte später korrigieren.'),
      (N'GJS500',    0.060, 0.350, 3.600, 2.000, 0.050, 0.045, 0.040, 0.080, 0.330, 0.370, 150.000, N'todo', N'Vorläufige Analyse, bitte später korrigieren.'),
      (N'GJS600',    0.070, 0.450, 3.650, 1.900, 0.060, 0.050, 0.050, 0.090, 0.430, 0.470, 150.000, N'todo', N'Vorläufige Analyse, bitte später korrigieren.'),
      (N'GJS700',    0.080, 0.550, 3.700, 1.800, 0.070, 0.055, 0.060, 0.100, 0.530, 0.570, 150.000, N'todo', N'Vorläufige Analyse, bitte später korrigieren.'),
      (N'GSL',       0.040, 0.300, 3.300, 1.700, 0.040, 0.030, 0.020, 0.060, 0.280, 0.320, 150.000, N'todo', N'Vorläufige Analyse, bitte später korrigieren.'),
      (N'GJS600-10', 0.070, 0.420, 3.600, 2.200, 0.060, 0.050, 0.050, 0.090, 0.400, 0.440, 150.000, N'todo', N'Vorläufige Analyse, bitte später korrigieren.'),
      (N'Pakete+',   0.120, 0.500, 0.200, 0.120, 0.080, 0.010, 0.100, 0.140, 0.480, 0.520, 150.000, N'todo', N'Vorläufige Analyse, bitte später korrigieren.'),
      (N'Pakete-',   0.030, 0.200, 0.150, 0.080, 0.030, 0.010, 0.010, 0.050, 0.180, 0.220, 150.000, N'todo', N'Vorläufige Analyse, bitte später korrigieren.'),
      (N'Stanz+',    0.100, 0.450, 0.180, 0.100, 0.070, 0.010, 0.080, 0.120, 0.430, 0.470, 150.000, N'todo', N'Vorläufige Analyse, bitte später korrigieren.'),
      (N'Stanz-',    0.020, 0.180, 0.120, 0.060, 0.020, 0.010, 0.000, 0.040, 0.160, 0.200, 150.000, N'todo', N'Vorläufige Analyse, bitte später korrigieren.'),
      (N'Spähne',    0.090, 0.650, 0.250, 0.140, 0.080, 0.010, 0.070, 0.110, 0.630, 0.670, 150.000, N'todo', N'Vorläufige Analyse, bitte später korrigieren.')
) AS source
(
   MaterialName,
   Cu,
   Mn,
   C,
   Si,
   Cr,
   Mg,
   Cu_Min,
   Cu_Max,
   Mn_Min,
   Mn_Max,
   Toleranz,
   Beschaffenheit,
   Bemerkung
)
ON target.MaterialName = source.MaterialName
WHEN MATCHED THEN
   UPDATE SET
      Cu = source.Cu,
      Mn = source.Mn,
      C = source.C,
      Si = source.Si,
      Cr = source.Cr,
      Mg = source.Mg,
      Cu_Min = source.Cu_Min,
      Cu_Max = source.Cu_Max,
      Mn_Min = source.Mn_Min,
      Mn_Max = source.Mn_Max,
      Toleranz = source.Toleranz,
      Beschaffenheit = source.Beschaffenheit,
      Bemerkung = source.Bemerkung
WHEN NOT MATCHED THEN
   INSERT
   (
      MaterialName,
      Cu,
      Mn,
      C,
      Si,
      Cr,
      Mg,
      Cu_Min,
      Cu_Max,
      Mn_Min,
      Mn_Max,
      Toleranz,
      Beschaffenheit,
      Bemerkung
   )
   VALUES
   (
      source.MaterialName,
      source.Cu,
      source.Mn,
      source.C,
      source.Si,
      source.Cr,
      source.Mg,
      source.Cu_Min,
      source.Cu_Max,
      source.Mn_Min,
      source.Mn_Max,
      source.Toleranz,
      source.Beschaffenheit,
      source.Bemerkung
   );

IF COL_LENGTH(N'dbo.FALCOM_LAGER', N'MaterialID') IS NULL
BEGIN
   ALTER TABLE dbo.FALCOM_LAGER ADD MaterialID bigint NULL;
END;

UPDATE lager
   SET MaterialID = material.ID
FROM dbo.FALCOM_LAGER AS lager
INNER JOIN dbo.FALCOM_MATERIAL AS material
   ON material.MaterialName =
      CASE lager.Lagerplatz
         WHEN 1 THEN N'GJS400'
         WHEN 2 THEN N'GJS500'
         WHEN 3 THEN N'Spähne'
         WHEN 4 THEN N'GJS600'
         WHEN 5 THEN N'Pakete+'
         WHEN 6 THEN N'Pakete-'
         WHEN 7 THEN N'Stanz+'
         WHEN 8 THEN N'Stanz-'
         WHEN 9 THEN N'GJS700'
         WHEN 10 THEN N'GSL'
         ELSE N'GJS400'
      END
WHERE lager.MaterialID IS NULL;

UPDATE lager
   SET MaterialID = material.ID
FROM dbo.FALCOM_LAGER AS lager
CROSS JOIN dbo.FALCOM_MATERIAL AS material
WHERE lager.MaterialID IS NULL
  AND material.MaterialName = N'GJS400';

UPDATE dbo.FALCOM_LAGER
   SET MaterialID = NULL
WHERE PlatzTyp = N'LKW_PLATZ'
   OR Lagerplatz IN (101,102,103);

IF OBJECT_ID(N'dbo.FK_FALCOM_LAGER_MATERIAL', N'F') IS NULL
BEGIN
   ALTER TABLE dbo.FALCOM_LAGER WITH CHECK
      ADD CONSTRAINT FK_FALCOM_LAGER_MATERIAL
      FOREIGN KEY(MaterialID)
      REFERENCES dbo.FALCOM_MATERIAL(ID);
END;

DECLARE @dropSql nvarchar(max) = N'';

SELECT @dropSql += N'ALTER TABLE dbo.FALCOM_LAGER DROP CONSTRAINT ' + QUOTENAME(dc.name) + N';' + CHAR(13)
FROM sys.default_constraints AS dc
INNER JOIN sys.columns AS c
   ON c.object_id = dc.parent_object_id
  AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.FALCOM_LAGER')
  AND c.name IN
  (
     N'Cu',
     N'Mn',
     N'C',
     N'Si',
     N'Cu_Min',
     N'Cu_Max',
     N'Mn_Min',
     N'Mn_Max',
     N'Schrottsorte',
     N'Toleranz'
  );

IF @dropSql <> N''
BEGIN
   EXEC sp_executesql @dropSql;
END;

IF COL_LENGTH(N'dbo.FALCOM_LAGER', N'Cu') IS NOT NULL
   ALTER TABLE dbo.FALCOM_LAGER DROP COLUMN Cu;
IF COL_LENGTH(N'dbo.FALCOM_LAGER', N'Mn') IS NOT NULL
   ALTER TABLE dbo.FALCOM_LAGER DROP COLUMN Mn;
IF COL_LENGTH(N'dbo.FALCOM_LAGER', N'C') IS NOT NULL
   ALTER TABLE dbo.FALCOM_LAGER DROP COLUMN C;
IF COL_LENGTH(N'dbo.FALCOM_LAGER', N'Si') IS NOT NULL
   ALTER TABLE dbo.FALCOM_LAGER DROP COLUMN Si;
IF COL_LENGTH(N'dbo.FALCOM_LAGER', N'Cu_Min') IS NOT NULL
   ALTER TABLE dbo.FALCOM_LAGER DROP COLUMN Cu_Min;
IF COL_LENGTH(N'dbo.FALCOM_LAGER', N'Cu_Max') IS NOT NULL
   ALTER TABLE dbo.FALCOM_LAGER DROP COLUMN Cu_Max;
IF COL_LENGTH(N'dbo.FALCOM_LAGER', N'Mn_Min') IS NOT NULL
   ALTER TABLE dbo.FALCOM_LAGER DROP COLUMN Mn_Min;
IF COL_LENGTH(N'dbo.FALCOM_LAGER', N'Mn_Max') IS NOT NULL
   ALTER TABLE dbo.FALCOM_LAGER DROP COLUMN Mn_Max;
IF COL_LENGTH(N'dbo.FALCOM_LAGER', N'Schrottsorte') IS NOT NULL
   ALTER TABLE dbo.FALCOM_LAGER DROP COLUMN Schrottsorte;
IF COL_LENGTH(N'dbo.FALCOM_LAGER', N'Toleranz') IS NOT NULL
   ALTER TABLE dbo.FALCOM_LAGER DROP COLUMN Toleranz;

COMMIT TRANSACTION;
GO

