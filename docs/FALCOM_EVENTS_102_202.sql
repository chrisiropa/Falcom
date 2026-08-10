USE [FG];
GO

SET XACT_ABORT ON;
GO

BEGIN TRY
   BEGIN TRANSACTION;

   IF NOT EXISTS (SELECT 1 FROM dbo.FALCOM_EVENTS WHERE ID = 102)
   BEGIN
      INSERT INTO dbo.FALCOM_EVENTS
      (
         ID,
         EventName,
         Direction,
         Description,
         IsActive
      )
      SELECT
         102,
         N'Event_102',
         Direction,
         Description,
         IsActive
      FROM dbo.FALCOM_EVENTS
      WHERE ID = 2;
   END;

   IF NOT EXISTS (SELECT 1 FROM dbo.FALCOM_EVENTS WHERE ID = 202)
   BEGIN
      INSERT INTO dbo.FALCOM_EVENTS
      (
         ID,
         EventName,
         Direction,
         Description,
         IsActive
      )
      SELECT
         202,
         N'Event_202',
         Direction,
         Description,
         IsActive
      FROM dbo.FALCOM_EVENTS
      WHERE ID = 1;
   END;

   IF NOT EXISTS (SELECT 1 FROM dbo.FALCOM_EVENTS WHERE ID = 102)
      THROW 51000, 'Event_102 konnte nicht aus Event 2 angelegt werden.', 1;

   IF NOT EXISTS (SELECT 1 FROM dbo.FALCOM_EVENTS WHERE ID = 202)
      THROW 51001, 'Event_202 konnte nicht aus Event 1 angelegt werden.', 1;

   UPDATE dbo.FALCOM_EVENTS
      SET EventName = N'Event_102'
    WHERE ID = 102;

   UPDATE dbo.FALCOM_EVENTS
      SET EventName = N'Event_202'
    WHERE ID = 202;

   UPDATE dbo.FALCOM_EVENT_OPC_NODES
      SET EventID = 102
    WHERE EventID = 2;

   UPDATE dbo.FALCOM_EVENT_OPC_NODES
      SET EventID = 202
    WHERE EventID = 1;

   UPDATE dbo.FALCOM_EVENT_OPC_NODES
      SET NodeName = N'Event_102',
          NodeRole = N'Trigger',
          DataType = N'Int32'
    WHERE EventID = 102
      AND (ID = 16 OR NodeName = N'TelegrammNummer');
   UPDATE dbo.FALCOM_EVENT_OPC_NODES
      SET NodeName = N'Event_202'
    WHERE EventID = 202
      AND NodeRole = N'Trigger';
   UPDATE dbo.FALCOM_EVENT_OPC_NODES
      SET NodeName = N'ZielPos'
    WHERE EventID = 102
      AND ID = 23;

   IF EXISTS
   (
      SELECT 1
      FROM dbo.FALCOM_EVENT_OPC_NODES
      GROUP BY EventID, NodeName
      HAVING COUNT(*) > 1
   )
      THROW 51002, 'Durch die NodeName-Umbenennung sind doppelte NodeNames entstanden.', 1;

   UPDATE dbo.FALCOM_Kran_SPS_SIM_EventZuordnung
      SET SourceEventID = 102
    WHERE SourceEventID = 2;

   UPDATE dbo.FALCOM_Kran_SPS_SIM_EventZuordnung
      SET SourceEventID = 202
    WHERE SourceEventID = 1;

   UPDATE dbo.FALCOM_Kran_SPS_SIM_EventZuordnung
      SET TargetEventID = 102
    WHERE TargetEventID = 2;

   UPDATE dbo.FALCOM_Kran_SPS_SIM_EventZuordnung
      SET TargetEventID = 202
    WHERE TargetEventID = 1;

   DELETE FROM dbo.FALCOM_EVENTS
    WHERE ID IN (1, 2);

   COMMIT TRANSACTION;
END TRY
BEGIN CATCH
   IF @@TRANCOUNT > 0
      ROLLBACK TRANSACTION;
   THROW;
END CATCH;
GO

SELECT
   e.ID,
   e.EventName,
   e.Direction,
   COUNT(n.ID) AS NodeCount
FROM dbo.FALCOM_EVENTS AS e
LEFT JOIN dbo.FALCOM_EVENT_OPC_NODES AS n
   ON n.EventID = e.ID
WHERE e.ID IN (102, 202)
GROUP BY
   e.ID,
   e.EventName,
   e.Direction
ORDER BY e.ID;
GO
