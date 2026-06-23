USE [FG];
GO

SET XACT_ABORT ON;
GO

BEGIN TRY
   BEGIN TRANSACTION;

   MERGE dbo.FALCOM_EVENTS AS target
   USING (VALUES
      (
         N'Watchdog',
         N'FALCOM->KRAN_SPS',
         N'Lebenszähler von FALCOM an die Kran-SPS. OPC-Node-ID ist noch zu konfigurieren.',
         CAST(0 AS bit)
      )
   ) AS source (EventName, Direction, Description, IsActive)
   ON target.EventName = source.EventName
   WHEN MATCHED THEN
      UPDATE SET
         target.Direction = source.Direction,
         target.Description = source.Description,
         target.IsActive = source.IsActive
   WHEN NOT MATCHED THEN
      INSERT (EventName, Direction, Description, IsActive)
      VALUES (
         source.EventName,
         source.Direction,
         source.Description,
         source.IsActive
      );

   DECLARE @EventID bigint =
      (SELECT ID
       FROM dbo.FALCOM_EVENTS
       WHERE EventName = N'Watchdog');

   MERGE dbo.FALCOM_EVENT_OPC_NODES AS target
   USING (VALUES
      (
         N'LebensZaehler',
         N'NOCH_ZU_KONFIGURIEREN.LebensZaehler',
         N'Trigger',
         N'Int32'
      )
   ) AS source (NodeName, OPC_Node, NodeRole, DataType)
   ON target.EventID = @EventID
      AND target.NodeName = source.NodeName
   WHEN MATCHED THEN
      UPDATE SET
         target.OPC_Node = source.OPC_Node,
         target.NodeRole = source.NodeRole,
         target.DataType = source.DataType,
         target.IsRequired = 1
   WHEN NOT MATCHED THEN
      INSERT (
         EventID,
         NodeName,
         OPC_Node,
         NodeRole,
         DataType,
         IsRequired
      )
      VALUES (
         @EventID,
         source.NodeName,
         source.OPC_Node,
         source.NodeRole,
         source.DataType,
         1
      );

   COMMIT TRANSACTION;
END TRY
BEGIN CATCH
   IF @@TRANCOUNT > 0
      ROLLBACK TRANSACTION;

   THROW;
END CATCH;
GO
