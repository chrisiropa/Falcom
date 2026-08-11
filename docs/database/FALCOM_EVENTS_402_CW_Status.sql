SET NOCOUNT ON;

IF NOT EXISTS (
   SELECT 1
   FROM dbo.FALCOM_EVENTS
   WHERE ID = 402
)
BEGIN
   INSERT INTO dbo.FALCOM_EVENTS (
      ID,
      EventName,
      Direction,
      Description,
      IsActive,
      Partner
   )
   VALUES (
      402,
      N'Event_402',
      N'CW->FALCOM',
      N'Statuswerte vom Chargierwagen an FALCOM.',
      1,
      N'CW'
   );
END
ELSE
BEGIN
   UPDATE dbo.FALCOM_EVENTS
      SET EventName = N'Event_402',
          Direction = N'CW->FALCOM',
          Description = N'Statuswerte vom Chargierwagen an FALCOM.',
          IsActive = 1,
          Partner = N'CW'
    WHERE ID = 402;
END;

DECLARE @Nodes TABLE (
   ID bigint NOT NULL,
   NodeName nvarchar(128) NOT NULL,
   NodeRole nvarchar(16) NOT NULL,
   OPC_Node nvarchar(1000) NOT NULL,
   DataType nvarchar(64) NULL,
   IsRequired bit NOT NULL,
   Beschreibung nvarchar(1000) NULL
);

INSERT INTO @Nodes (
   ID,
   NodeName,
   NodeRole,
   OPC_Node,
   DataType,
   IsRequired,
   Beschreibung
)
VALUES
   (402001, N'Event_402', N'Trigger', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.EZ_Status', N'Int32', 1, N'Ereigniszaehler fuer Statuswerte vom Chargierwagen an FALCOM.'),
   (402002, N'Istgew_ChW1', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.Status.Istgew_ChW1', N'Int32', 1, N'Istgewicht Chargierwagen 1.'),
   (402003, N'Istgew_ChW2', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.Status.Istgew_ChW2', N'Int32', 1, N'Istgewicht Chargierwagen 2.'),
   (402004, N'Istgew_ChW3', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.Status.Istgew_ChW3', N'Int32', 1, N'Istgewicht Chargierwagen 3.');

MERGE dbo.FALCOM_EVENT_OPC_NODES AS target
USING @Nodes AS source
   ON target.EventID = 402
  AND target.NodeName = source.NodeName
WHEN MATCHED THEN
   UPDATE SET
      target.ID = source.ID,
      target.NodeRole = source.NodeRole,
      target.OPC_Node = source.OPC_Node,
      target.DataType = source.DataType,
      target.IsRequired = source.IsRequired,
      target.Beschreibung = source.Beschreibung
WHEN NOT MATCHED BY TARGET THEN
   INSERT (
      ID,
      EventID,
      NodeName,
      OPC_Node,
      NodeRole,
      DataType,
      IsRequired,
      Beschreibung
   )
   VALUES (
      source.ID,
      402,
      source.NodeName,
      source.OPC_Node,
      source.NodeRole,
      source.DataType,
      source.IsRequired,
      source.Beschreibung
   );
