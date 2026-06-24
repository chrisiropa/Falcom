USE [FG]
GO

/*
   Korrektur für dbo.FALCOM_TryCreateNextAktuelleFahrt:

   Wenn @AuftragID übergeben wird, stammt der Auslöser aus dbo.FALCOM_AUFTRAG.
   Dann darf die Prozedur keinen offenen Einlagerauftrag bevorzugen, sondern muss
   zwingend eine CHARGIEREN-Fahrt für genau diesen Auftrag erzeugen.

   Einlageraufträge werden nur automatisch gezogen, wenn @AuftragID NULL ist.
*/

-- Die Prozedur ist in FG.dbo entsprechend angepasst.
