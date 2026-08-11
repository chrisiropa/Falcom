SET NOCOUNT ON;

IF NOT EXISTS (
   SELECT 1
   FROM dbo.FALCOM_EVENTS
   WHERE ID = 404
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
      404,
      N'Event_404',
      N'CW->FALCOM',
      N'Stoerungswerte vom Chargierwagen an FALCOM.',
      1,
      N'CW'
   );
END
ELSE
BEGIN
   UPDATE dbo.FALCOM_EVENTS
      SET EventName = N'Event_404',
          Direction = N'CW->FALCOM',
          Description = N'Stoerungswerte vom Chargierwagen an FALCOM.',
          IsActive = 1,
          Partner = N'CW'
    WHERE ID = 404;
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
   (404001, N'Event_404', N'Trigger', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.EZ_Stoerung', N'Int32', 1, N'Ereigniszaehler fuer Stoerungswerte vom Chargierwagen an FALCOM.'),
   (404002, N'Stoerung_ChW1', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.Stoerung.Stoerung_ChW1', N'Boolean', 1, N'Stoerung Chargierwagen 1.'),
   (404003, N'Stoerung_ChW2', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.Stoerung.Stoerung_ChW2', N'Boolean', 1, N'Stoerung Chargierwagen 2.'),
   (404004, N'Stoerung_ChW3', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.Stoerung.Stoerung_ChW3', N'Boolean', 1, N'Stoerung Chargierwagen 3.');

DELETE FROM dbo.FALCOM_EVENT_OPC_NODES
 WHERE EventID = 404
   AND NodeName IN (N'Steuerung_ChW1', N'Steuerung_ChW2', N'Steuerung_ChW3');

MERGE dbo.FALCOM_EVENT_OPC_NODES AS target
USING @Nodes AS source
   ON target.EventID = 404
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
      404,
      source.NodeName,
      source.OPC_Node,
      source.NodeRole,
      source.DataType,
      source.IsRequired,
      source.Beschreibung
   );
