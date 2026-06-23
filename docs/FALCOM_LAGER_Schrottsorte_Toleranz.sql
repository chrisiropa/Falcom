USE [FG];
GO

SET XACT_ABORT ON;
GO

BEGIN TRY
   BEGIN TRANSACTION;

   IF COL_LENGTH(N'dbo.FALCOM_LAGER', N'Schrottsorte') IS NULL
   BEGIN
      ALTER TABLE dbo.FALCOM_LAGER
         ADD Schrottsorte nvarchar(128) NOT NULL
            CONSTRAINT DF_FALCOM_LAGER_Schrottsorte
            DEFAULT (N'Unbekannt');
   END;

   IF COL_LENGTH(N'dbo.FALCOM_LAGER', N'Toleranz') IS NULL
   BEGIN
      ALTER TABLE dbo.FALCOM_LAGER
         ADD Toleranz int NOT NULL
            CONSTRAINT DF_FALCOM_LAGER_Toleranz
            DEFAULT (150);
   END;

   EXEC sys.sp_executesql N'
      UPDATE dbo.FALCOM_LAGER
      SET Schrottsorte =
         CASE Lagerplatz
            WHEN 1 THEN N''Leitplanken''
            WHEN 2 THEN N''Roheisen''
            WHEN 3 THEN N''Stahlspäne''
            WHEN 4 THEN N''Schwerschrott''
            WHEN 5 THEN N''Blechschrott''
            WHEN 6 THEN N''Gussschrott''
            WHEN 7 THEN N''Mischschrott''
            WHEN 8 THEN N''Stanzabfälle''
            WHEN 9 THEN N''Träger und Profile''
            WHEN 10 THEN N''Kreislaufschrott''
            WHEN 101 THEN N''Leitplanken''
            WHEN 102 THEN N''Stahlspäne''
            WHEN 103 THEN N''Roheisen''
            ELSE N''Unbekannt''
         END,
         Toleranz = 150;';

   COMMIT TRANSACTION;
END TRY
BEGIN CATCH
   IF @@TRANCOUNT > 0
      ROLLBACK TRANSACTION;

   THROW;
END CATCH;
GO
