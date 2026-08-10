USE [FG];
GO

SET XACT_ABORT ON;
GO

BEGIN TRY
   BEGIN TRANSACTION;

   IF COL_LENGTH(N'dbo.FALCOM_EVENT_OPC_NODES', N'MeissnerNodeName') IS NOT NULL
   BEGIN
      IF EXISTS
      (
         SELECT 1
         FROM sys.sql_expression_dependencies
         WHERE referenced_id = OBJECT_ID(N'dbo.FALCOM_EVENT_OPC_NODES')
           AND referenced_minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.FALCOM_EVENT_OPC_NODES'), N'MeissnerNodeName', 'ColumnId')
      )
         THROW 51020, 'MeissnerNodeName kann nicht entfernt werden, weil SQL-Abhaengigkeiten existieren.', 1;

      ALTER TABLE dbo.FALCOM_EVENT_OPC_NODES
         DROP COLUMN MeissnerNodeName;
   END;

   COMMIT TRANSACTION;
END TRY
BEGIN CATCH
   IF @@TRANCOUNT > 0
      ROLLBACK TRANSACTION;
   THROW;
END CATCH;
GO

SELECT
   ColumnExists = COUNT_BIG(*)
FROM sys.columns
WHERE object_id = OBJECT_ID(N'dbo.FALCOM_EVENT_OPC_NODES')
  AND name = N'MeissnerNodeName';
GO
