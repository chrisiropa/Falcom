SET NOCOUNT ON;

IF NOT EXISTS (
   SELECT 1
   FROM dbo.FALCOM_EVENTS
   WHERE ID = 405
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
      405,
      N'Event_405',
      N'CW->FALCOM',
      N'Meldung Gattierung abgeschlossen vom Chargierwagen an FALCOM.',
      1,
      N'CW'
   );
END
ELSE
BEGIN
   UPDATE dbo.FALCOM_EVENTS
      SET EventName = N'Event_405',
          Direction = N'CW->FALCOM',
          Description = N'Meldung Gattierung abgeschlossen vom Chargierwagen an FALCOM.',
          IsActive = 1,
          Partner = N'CW'
    WHERE ID = 405;
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
   (405001, N'Event_405', N'Trigger', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.EZ_GattierungAbgeschl', N'Int32', 1, N'Ereigniszaehler fuer die Meldung Gattierung abgeschlossen vom Chargierwagen an FALCOM.'),
   (405002, N'GattierungAbgeschl', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.GattierungAbgeschl.GattierungAbgeschl', N'Boolean', 1, N'Meldet, dass die Gattierung abgeschlossen ist.'),
   (405003, N'C', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.GattierungAbgeschl.C', N'Single', 1, N'Istwert Kohlenstoff.'),
   (405004, N'Si', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.GattierungAbgeschl.Si', N'Single', 1, N'Istwert Silizium.'),
   (405005, N'MN', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.GattierungAbgeschl.MN', N'Single', 1, N'Istwert Mangan.'),
   (405006, N'Cu', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.GattierungAbgeschl.Cu', N'Single', 1, N'Istwert Kupfer.'),
   (405007, N'ChW_ID', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.GattierungAbgeschl.ChW_ID', N'Int32', 1, N'Chargierwagen-ID zur Meldung Gattierung abgeschlossen.'),
   (405008, N'AuftragsNr', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.Out.GattierungAbgeschl.AuftragsNr', N'Int32', 1, N'FALCOM-Auftragsnummer zur Meldung Gattierung abgeschlossen.');

MERGE dbo.FALCOM_EVENT_OPC_NODES AS target
USING @Nodes AS source
   ON target.EventID = 405
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
      405,
      source.NodeName,
      source.OPC_Node,
      source.NodeRole,
      source.DataType,
      source.IsRequired,
      source.Beschreibung
   );
