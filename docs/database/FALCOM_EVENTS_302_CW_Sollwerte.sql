SET NOCOUNT ON;

IF NOT EXISTS (
   SELECT 1
   FROM dbo.FALCOM_EVENTS
   WHERE ID = 302
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
      302,
      N'Event_302',
      N'FALCOM->CW',
      N'Sollwerte von FALCOM an den Chargierwagen.',
      1,
      N'CW'
   );
END
ELSE
BEGIN
   UPDATE dbo.FALCOM_EVENTS
      SET EventName = N'Event_302',
          Direction = N'FALCOM->CW',
          Description = N'Sollwerte von FALCOM an den Chargierwagen.',
          IsActive = 1,
          Partner = N'CW'
    WHERE ID = 302;
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
   (302001, N'Event_302', N'Trigger', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.In.EZ_Sollwerte', N'Int32', 1, N'Ereigniszaehler fuer Sollwerte von FALCOM an den Chargierwagen. Wird nach dem Schreiben der Payload hochgezaehlt.'),
   (302002, N'C', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.In.Sollwerte.C', N'Single', 1, N'Sollwert Kohlenstoff.'),
   (302003, N'Si', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.In.Sollwerte.Si', N'Single', 1, N'Sollwert Silizium.'),
   (302004, N'MN', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.In.Sollwerte.MN', N'Single', 1, N'Sollwert Mangan.'),
   (302005, N'Cu', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.In.Sollwerte.Cu', N'Single', 1, N'Sollwert Kupfer.'),
   (302006, N'ChW_ID', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.In.Sollwerte.ChW_ID', N'Int32', 1, N'Chargierwagen-ID, fuer die diese Sollwerte gelten.'),
   (302007, N'AuftragsNr', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.In.Sollwerte.AuftragsNr', N'Int32', 1, N'FALCOM-Auftragsnummer zu den Sollwerten.');

MERGE dbo.FALCOM_EVENT_OPC_NODES AS target
USING @Nodes AS source
   ON target.EventID = 302
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
      302,
      source.NodeName,
      source.OPC_Node,
      source.NodeRole,
      source.DataType,
      source.IsRequired,
      source.Beschreibung
   );
