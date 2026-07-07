USE [FG]
GO

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.FALCOM_MATERIAL', N'U') IS NULL
BEGIN
   THROW 51000, 'Tabelle dbo.FALCOM_MATERIAL existiert nicht.', 1;
END;

IF COL_LENGTH(N'dbo.FALCOM_MATERIAL', N'ID1') IS NULL
BEGIN
   THROW 51001, 'Spalte ID1 existiert in dbo.FALCOM_MATERIAL nicht.', 1;
END;

IF EXISTS (SELECT 1 FROM dbo.FALCOM_MATERIAL WHERE ID1 IS NULL)
BEGIN
   THROW 51002, 'ID1 darf nicht NULL sein.', 1;
END;

IF EXISTS
(
   SELECT 1
   FROM dbo.FALCOM_MATERIAL
   GROUP BY ID1
   HAVING COUNT(*) > 1
)
BEGIN
   THROW 51003, 'ID1 ist nicht eindeutig.', 1;
END;

DECLARE @dropFkSql nvarchar(max) = N'';

SELECT @dropFkSql += N'ALTER TABLE ' + QUOTENAME(SCHEMA_NAME(parent.schema_id)) + N'.' + QUOTENAME(parent.name) +
                     N' DROP CONSTRAINT ' + QUOTENAME(fk.name) + N';' + CHAR(13)
FROM sys.foreign_keys AS fk
INNER JOIN sys.tables AS parent
   ON parent.object_id = fk.parent_object_id
WHERE fk.referenced_object_id = OBJECT_ID(N'dbo.FALCOM_MATERIAL');

IF @dropFkSql <> N''
BEGIN
   EXEC sp_executesql @dropFkSql;
END;

IF OBJECT_ID(N'dbo.FALCOM_MATERIAL_NEU', N'U') IS NOT NULL
BEGIN
   DROP TABLE dbo.FALCOM_MATERIAL_NEU;
END;

CREATE TABLE dbo.FALCOM_MATERIAL_NEU
(
   ID bigint IDENTITY(1,1) NOT NULL
      CONSTRAINT PK_FALCOM_MATERIAL_NEU PRIMARY KEY,
   MaterialName nvarchar(128) NOT NULL,
   Cu decimal(10,3) NOT NULL
      CONSTRAINT DF_FALCOM_MATERIAL_NEU_Cu DEFAULT 0,
   Mn decimal(10,3) NOT NULL
      CONSTRAINT DF_FALCOM_MATERIAL_NEU_Mn DEFAULT 0,
   C decimal(10,3) NOT NULL
      CONSTRAINT DF_FALCOM_MATERIAL_NEU_C DEFAULT 0,
   Si decimal(10,3) NOT NULL
      CONSTRAINT DF_FALCOM_MATERIAL_NEU_Si DEFAULT 0,
   Cr decimal(10,3) NOT NULL
      CONSTRAINT DF_FALCOM_MATERIAL_NEU_Cr DEFAULT 0,
   Mg decimal(10,3) NOT NULL
      CONSTRAINT DF_FALCOM_MATERIAL_NEU_Mg DEFAULT 0,
   Cu_Min decimal(10,3) NOT NULL
      CONSTRAINT DF_FALCOM_MATERIAL_NEU_CuMin DEFAULT 0,
   Cu_Max decimal(10,3) NOT NULL
      CONSTRAINT DF_FALCOM_MATERIAL_NEU_CuMax DEFAULT 0,
   Mn_Min decimal(10,3) NOT NULL
      CONSTRAINT DF_FALCOM_MATERIAL_NEU_MnMin DEFAULT 0,
   Mn_Max decimal(10,3) NOT NULL
      CONSTRAINT DF_FALCOM_MATERIAL_NEU_MnMax DEFAULT 0,
   Toleranz decimal(12,3) NOT NULL
      CONSTRAINT DF_FALCOM_MATERIAL_NEU_Toleranz DEFAULT 150,
   Beschaffenheit nvarchar(128) NOT NULL
      CONSTRAINT DF_FALCOM_MATERIAL_NEU_Beschaffenheit DEFAULT N'todo',
   Bemerkung nvarchar(1024) NULL
);

CREATE UNIQUE INDEX UX_FALCOM_MATERIAL_NEU_MaterialName
   ON dbo.FALCOM_MATERIAL_NEU(MaterialName);

SET IDENTITY_INSERT dbo.FALCOM_MATERIAL_NEU ON;

INSERT dbo.FALCOM_MATERIAL_NEU
(
   ID,
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
SELECT
   ID1,
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
FROM dbo.FALCOM_MATERIAL
ORDER BY ID1;

SET IDENTITY_INSERT dbo.FALCOM_MATERIAL_NEU OFF;

UPDATE lager
   SET MaterialID = material.ID1
FROM dbo.FALCOM_LAGER AS lager
INNER JOIN dbo.FALCOM_MATERIAL AS material
   ON material.ID = lager.MaterialID;

UPDATE dbo.FALCOM_LAGER
   SET MaterialID = NULL
WHERE PlatzTyp = N'LKW_PLATZ'
   OR Lagerplatz IN (101,102,103);

DROP TABLE dbo.FALCOM_MATERIAL;

EXEC sp_rename N'dbo.FALCOM_MATERIAL_NEU', N'FALCOM_MATERIAL';

ALTER TABLE dbo.FALCOM_LAGER WITH CHECK
   ADD CONSTRAINT FK_FALCOM_LAGER_MATERIAL
   FOREIGN KEY(MaterialID)
   REFERENCES dbo.FALCOM_MATERIAL(ID);

COMMIT TRANSACTION;
GO




