USE [FG]
GO

/*
   Korrektur für dbo.FALCOM_TryCreateNextAktuelleFahrt:

   QuellePositionID und ZielPositionID in dbo.FALCOM_AKTUELLE_FAHRT sind
   technische Fremdschlüssel auf dbo.FALCOM_KRAN_POSITION.ID.

   Deshalb darf QuellePositionID bei Chargierfahrten nicht direkt mit
   FALCOM_AUFTRAG_BERECHNET.BoxID befüllt werden.

   Mapping:
   - FALCOM_AUFTRAG_BERECHNET.BoxID 1..10
       -> FALCOM_KRAN_POSITION.PositionsTyp = 'LAGERBOX'
       -> FALCOM_KRAN_POSITION.PositionsNr = BoxID

   - FALCOM_AUFTRAG_BERECHNET.BoxID 101..103
       -> FALCOM_KRAN_POSITION.PositionsTyp = 'LKW_PLATZ'
       -> FALCOM_KRAN_POSITION.PositionsNr = BoxID - 100

   - FALCOM_AUFTRAG.ChargierwagenNr
       -> FALCOM_KRAN_POSITION.PositionsTyp = 'CHARGIERWAGEN'
       -> FALCOM_KRAN_POSITION.PositionsNr = ChargierwagenNr
*/

-- Die Prozedur dbo.FALCOM_TryCreateNextAktuelleFahrt ist in FG.dbo entsprechend angepasst.
