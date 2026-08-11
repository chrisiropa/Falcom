SET NOCOUNT ON;

IF NOT EXISTS (
   SELECT 1
   FROM dbo.FALCOM_EVENTS
   WHERE ID = 401
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
      401,
      N'Event_401',
      N'CW->FALCOM',
      N'Lebenszaehler vom Chargierwagen an FALCOM.',
      1,
      N'CW'
   );
END
ELSE
BEGIN
   UPDATE dbo.FALCOM_EVENTS
      SET EventName = N'Event_401',
          Direction = N'CW->FALCOM',
          Description = N'Lebenszaehler vom Chargierwagen an FALCOM.',
          IsActive = 1,
          Partner = N'CW'
    WHERE ID = 401;
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
   (401001, N'Event_401', N'Trigger', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.EZ_Lebenszaehler', N'Int32', 1, N'DINT-Lebenszaehler vom Chargierwagen an FALCOM. Der Triggerwert ist zugleich der Nutzwert.');

DELETE FROM dbo.FALCOM_EVENT_OPC_NODES
 WHERE EventID = 401
   AND NodeName <> N'Event_401';

MERGE dbo.FALCOM_EVENT_OPC_NODES AS target
USING @Nodes AS source
   ON target.EventID = 401
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
      401,
      source.NodeName,
      source.OPC_Node,
      source.NodeRole,
      source.DataType,
      source.IsRequired,
      source.Beschreibung
   );
