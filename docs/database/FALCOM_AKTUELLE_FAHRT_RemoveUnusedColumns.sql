USE [FG]
GO

/*
   dbo.FALCOM_AKTUELLE_FAHRT ist ein 1-Slot-Kanal.

   Entfernt wurden deshalb:
   - Reihenfolge
   - Prioritaet
   - Bearbeiter
   - FehlerDatumZeit
   - FehlerText

   Der alte Sortierindex IX_FALCOM_KRAN_QUEUE_NaechsteFahrt wurde ebenfalls
   entfernt, weil Sortierung/Priorisierung bei genau einem Slot keine Bedeutung hat.
*/

SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

IF EXISTS (
   SELECT 1
   FROM sys.indexes
   WHERE object_id = OBJECT_ID(N'dbo.FALCOM_AKTUELLE_FAHRT')
     AND name = N'IX_FALCOM_KRAN_QUEUE_NaechsteFahrt')
BEGIN
   DROP INDEX IX_FALCOM_KRAN_QUEUE_NaechsteFahrt
      ON dbo.FALCOM_AKTUELLE_FAHRT;
END

DECLARE @sql nvarchar(max);

SELECT @sql = N'ALTER TABLE dbo.FALCOM_AKTUELLE_FAHRT DROP CONSTRAINT ' + QUOTENAME(dc.name)
FROM sys.default_constraints AS dc
INNER JOIN sys.columns AS c
   ON c.object_id = dc.parent_object_id
  AND c.column_id = dc.parent_column_id
WHERE dc.parent_object_id = OBJECT_ID(N'dbo.FALCOM_AKTUELLE_FAHRT')
  AND c.name = N'Prioritaet';

IF @sql IS NOT NULL
   EXEC sp_executesql @sql;

IF COL_LENGTH(N'dbo.FALCOM_AKTUELLE_FAHRT', N'Prioritaet') IS NOT NULL
   ALTER TABLE dbo.FALCOM_AKTUELLE_FAHRT DROP COLUMN Prioritaet;

IF COL_LENGTH(N'dbo.FALCOM_AKTUELLE_FAHRT', N'Bearbeiter') IS NOT NULL
   ALTER TABLE dbo.FALCOM_AKTUELLE_FAHRT DROP COLUMN Bearbeiter;

IF COL_LENGTH(N'dbo.FALCOM_AKTUELLE_FAHRT', N'Reihenfolge') IS NOT NULL
   ALTER TABLE dbo.FALCOM_AKTUELLE_FAHRT DROP COLUMN Reihenfolge;

IF COL_LENGTH(N'dbo.FALCOM_AKTUELLE_FAHRT', N'FehlerText') IS NOT NULL
   ALTER TABLE dbo.FALCOM_AKTUELLE_FAHRT DROP COLUMN FehlerText;

IF COL_LENGTH(N'dbo.FALCOM_AKTUELLE_FAHRT', N'FehlerDatumZeit') IS NOT NULL
   ALTER TABLE dbo.FALCOM_AKTUELLE_FAHRT DROP COLUMN FehlerDatumZeit;

COMMIT TRANSACTION;
GO
