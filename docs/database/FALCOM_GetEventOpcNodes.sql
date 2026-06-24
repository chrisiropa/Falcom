USE [FG]
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_GetEventOpcNodes
   @EventName nvarchar(128),
   @Direction nvarchar(64)
AS
BEGIN
   SET NOCOUNT ON;

   SELECT
      nodes.NodeName,
      nodes.NodeRole,
      nodes.OPC_Node,
      nodes.DataType,
      nodes.IsRequired
   FROM dbo.FALCOM_EVENTS AS events
   INNER JOIN dbo.FALCOM_EVENT_OPC_NODES AS nodes
      ON nodes.EventID = events.ID
   WHERE events.EventName = @EventName
     AND events.Direction = @Direction
     AND nodes.IsRequired = 1
   ORDER BY nodes.ID;
END
GO
