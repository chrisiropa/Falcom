SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_SIM_GetEinlagerTeilfahrtStand
   @EinlagerAuftragID bigint,
   @AuftragTeilfahrt int
AS
BEGIN
   SET NOCOUNT ON;

   SELECT
      COUNT_BIG(*) AS HistorisierteTeilfahrten,
      CAST(
         CASE WHEN EXISTS
         (
            SELECT 1
            FROM dbo.FALCOM_EINLAGER_AUFTRAG_PRODUKTION_DETAIL AS currentDetail
            WHERE currentDetail.LagerAuftragID = @EinlagerAuftragID
              AND currentDetail.HubNr = @AuftragTeilfahrt
         )
         THEN 1 ELSE 0 END
         AS bit) AS AktuelleTeilfahrtBereitsHistorisiert
   FROM dbo.FALCOM_EINLAGER_AUFTRAG_PRODUKTION_DETAIL AS detail
   WHERE detail.LagerAuftragID = @EinlagerAuftragID;
END;
GO
