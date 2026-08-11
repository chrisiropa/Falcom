SET NOCOUNT ON;

IF NOT EXISTS (
   SELECT 1
   FROM dbo.FALCOM_EVENTS
   WHERE ID = 403
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
      403,
      N'Event_403',
      N'CW->FALCOM',
      N'Meldung Beladebereit vom Chargierwagen an FALCOM.',
      1,
      N'CW'
   );
END
ELSE
BEGIN
   UPDATE dbo.FALCOM_EVENTS
      SET EventName = N'Event_403',
          Direction = N'CW->FALCOM',
          Description = N'Meldung Beladebereit vom Chargierwagen an FALCOM.',
          IsActive = 1,
          Partner = N'CW'
    WHERE ID = 403;
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
   (403001, N'Event_403', N'Trigger', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.EZ_Beladebereit', N'Int32', 1, N'Ereigniszaehler fuer die Meldung Beladebereit vom Chargierwagen an FALCOM.'),
   (403002, N'Beladebereit_ChW1', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.Beladebereit.Beladebereit_ChW1', N'Boolean', 1, N'Chargierwagen 1 ist beladebereit.'),
   (403003, N'Beladebereit_ChW2', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.Beladebereit.Beladebereit_ChW2', N'Boolean', 1, N'Chargierwagen 2 ist beladebereit.'),
   (403004, N'Beladebereit_ChW3', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.Beladebereit.Beladebereit_ChW3', N'Boolean', 1, N'Chargierwagen 3 ist beladebereit.');

MERGE dbo.FALCOM_EVENT_OPC_NODES AS target
USING @Nodes AS source
   ON target.EventID = 403
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
      403,
      source.NodeName,
      source.OPC_Node,
      source.NodeRole,
      source.DataType,
      source.IsRequired,
      source.Beschreibung
   );
