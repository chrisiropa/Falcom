USE [FG]
GO

/*
   1-Slot-Fahrlogik:
   - dbo.FALCOM_AKTUELLE_FAHRT enthält maximal die aktuell aktive Fahrt.
   - Die Tabelle ist der logische Handshake zwischen FALCOM und Kran-SPS.
   - Fahrtabschluss wird atomar historisiert und aus dem Slot gelöscht.
   - Das Log speichert die ehemalige AktuelleFahrtID nur noch als Wert,
     nicht mehr als Foreign Key auf den zu löschenden Slot.
*/

IF COL_LENGTH(N'dbo.FALCOM_KRAN_COMMAND_LOG', N'AktuelleFahrtID') IS NULL
   AND COL_LENGTH(N'dbo.FALCOM_KRAN_COMMAND_LOG', N'KranQueueID') IS NOT NULL
BEGIN
   EXEC sp_rename N'dbo.FALCOM_KRAN_COMMAND_LOG.KranQueueID', N'AktuelleFahrtID', N'COLUMN';
END
GO

IF OBJECT_ID(N'dbo.FK_FALCOM_KRAN_COMMAND_LOG_AktuelleFahrt', N'F') IS NOT NULL
BEGIN
   ALTER TABLE dbo.FALCOM_KRAN_COMMAND_LOG
      DROP CONSTRAINT FK_FALCOM_KRAN_COMMAND_LOG_AktuelleFahrt;
END
GO

-- Die vollständigen Prozedurdefinitionen sind in der Datenbank installiert:
-- dbo.FALCOM_UpdateOrderStatus
-- dbo.FALCOM_IsOrderInProgress
-- dbo.FALCOM_TryCreateNextAktuelleFahrt
-- dbo.FALCOM_CompleteAktuelleFahrt
--
-- Hinweis: Dieses Dokument hält bewusst die Migrationsabsicht fest.
-- Die Prozeduren wurden direkt auf FG.dbo erstellt/geändert.
