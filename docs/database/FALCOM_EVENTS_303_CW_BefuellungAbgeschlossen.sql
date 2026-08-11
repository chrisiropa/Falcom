SET NOCOUNT ON;

IF NOT EXISTS (
   SELECT 1
   FROM dbo.FALCOM_EVENTS
   WHERE ID = 303
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
      303,
      N'Event_303',
      N'FALCOM->CW',
      N'Meldung Befuellung abgeschlossen von FALCOM an den Chargierwagen.',
      1,
      N'CW'
   );
END
ELSE
BEGIN
   UPDATE dbo.FALCOM_EVENTS
      SET EventName = N'Event_303',
          Direction = N'FALCOM->CW',
          Description = N'Meldung Befuellung abgeschlossen von FALCOM an den Chargierwagen.',
          IsActive = 1,
          Partner = N'CW'
    WHERE ID = 303;
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
   (303001, N'Event_303', N'Trigger', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.In.EZ_Befuellung_Abgeschl', N'Int32', 1, N'Ereigniszaehler fuer die Meldung Befuellung abgeschlossen von FALCOM an den Chargierwagen. Wird nach dem Schreiben der Payload hochgezaehlt.'),
   (303002, N'BefuellungAbgeschl', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.In.BefuellungAbgeschl.BefuellungAbgeschl', N'Boolean', 1, N'Meldet dem Chargierwagen, dass die Befuellung abgeschlossen ist.'),
   (303003, N'ChW_ID', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.In.BefuellungAbgeschl.ChW_ID', N'Int32', 1, N'Chargierwagen-ID, fuer die die Meldung gilt.'),
   (303004, N'AuftragsNr', N'Payload', N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.In.BefuellungAbgeschl.AuftragsNr', N'Int32', 1, N'FALCOM-Auftragsnummer zur Meldung Befuellung abgeschlossen.');

MERGE dbo.FALCOM_EVENT_OPC_NODES AS target
USING @Nodes AS source
   ON target.EventID = 303
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
      303,
      source.NodeName,
      source.OPC_Node,
      source.NodeRole,
      source.DataType,
      source.IsRequired,
      source.Beschreibung
   );
