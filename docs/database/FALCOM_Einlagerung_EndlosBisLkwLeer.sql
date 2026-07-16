USE [FG]
GO

SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

/*
   Stand: 2026-07-15

   Fachliche Änderung:
   - Einlagerfahrten werden nicht mehr anhand einer Sollmenge beendet.
   - Die frühere Einlager-Spalte SollMengeKg heißt fachlich jetzt ErwarteteMengeKg.
   - Jede zurückgemeldete Einlager-Teilfahrt wird in FALCOM_EINLAGER_AUFTRAG_DETAIL historisiert.
   - FALCOM_EINLAGER_AUFTRAG.IstMengeKg wird weiterhin als Kontrollwert hochgezählt.
   - DatumZeitFertig bleibt unverändert, bis später ein echtes "LKW Platz leer"-Ereignis verarbeitet wird.

   Hinweis:
   Die produktiven Prozeduren dbo.FALCOM_TryCreateNextAktuelleFahrt und
   dbo.FALCOM_CompleteAktuelleFahrt wurden am 2026-07-15 live entsprechend angepasst.
   Dieses Skript dokumentiert die fachliche Migration; bei einer Neuinstallation müssen
   die vollständigen Prozedurdefinitionen aus der Datenbank übernommen werden.
*/

PRINT N'FALCOM Einlagerung: Endlosbetrieb bis LKW-leer-Ereignis ist dokumentiert.';
GO
