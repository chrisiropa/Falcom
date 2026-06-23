USE [FG];
GO

SET XACT_ABORT ON;
GO

BEGIN TRY
   BEGIN TRANSACTION;

   IF COL_LENGTH(N'dbo.FALCOM_EVENTS', N'Source') IS NOT NULL
      AND COL_LENGTH(N'dbo.FALCOM_EVENTS', N'Direction') IS NULL
   BEGIN
      EXEC sys.sp_rename
         @objname = N'dbo.FALCOM_EVENTS.Source',
         @newname = N'Direction',
         @objtype = N'COLUMN';
   END;

   EXEC sys.sp_executesql N'
      UPDATE dbo.FALCOM_EVENTS
      SET Direction =
         CASE
            WHEN EventName LIKE N''KranSps%''
               THEN N''FALCOM->KRAN_SPS''
            ELSE N''KRAN_SPS->FALCOM''
         END
      WHERE Direction IS NULL
         OR Direction NOT IN (
            N''FALCOM->KRAN_SPS'',
            N''KRAN_SPS->FALCOM'');

      ALTER TABLE dbo.FALCOM_EVENTS
         ALTER COLUMN Direction nvarchar(128) NOT NULL;

      IF OBJECT_ID(
         N''dbo.CK_FALCOM_EVENTS_Direction'',
         N''C'') IS NULL
      BEGIN
         ALTER TABLE dbo.FALCOM_EVENTS
            ADD CONSTRAINT CK_FALCOM_EVENTS_Direction
            CHECK (
               Direction IN (
                  N''FALCOM->KRAN_SPS'',
                  N''KRAN_SPS->FALCOM''));
      END;';

   COMMIT TRANSACTION;
END TRY
BEGIN CATCH
   IF @@TRANCOUNT > 0
      ROLLBACK TRANSACTION;

   THROW;
END CATCH;
GO
