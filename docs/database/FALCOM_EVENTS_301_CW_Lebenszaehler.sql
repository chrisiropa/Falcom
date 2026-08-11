SET NOCOUNT ON;

IF NOT EXISTS (
   SELECT 1
   FROM dbo.FALCOM_EVENTS
   WHERE ID = 301
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
      301,
      N'Event_301',
      N'FALCOM->CW',
      N'Lebenszaehler von FALCOM an die CW-SPS.',
      1,
      N'CW'
   );
END
ELSE
BEGIN
   UPDATE dbo.FALCOM_EVENTS
      SET EventName = N'Event_301',
          Direction = N'FALCOM->CW',
          Description = N'Lebenszaehler von FALCOM an die CW-SPS.',
          IsActive = 1,
          Partner = N'CW'
    WHERE ID = 301;
END;

IF NOT EXISTS (
   SELECT 1
   FROM dbo.FALCOM_EVENT_OPC_NODES
   WHERE EventID = 301
     AND NodeName = N'Event_301'
)
BEGIN
   INSERT INTO dbo.FALCOM_EVENT_OPC_NODES (
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
      301001,
      301,
      N'Event_301',
      N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.In.EZ_Lebenszaehler',
      N'Trigger',
      N'Int32',
      1,
      N'DINT-Lebenszaehler von FALCOM zur CW-SPS. Wird sekündlich hochgezaehlt und dient als Lebenszeichen.'
   );
END
ELSE
BEGIN
   UPDATE dbo.FALCOM_EVENT_OPC_NODES
      SET OPC_Node = N'ns=1;s=CW.DataBlocks.DB_OPC_Komm.In.EZ_Lebenszaehler',
          NodeRole = N'Trigger',
          DataType = N'Int32',
          IsRequired = 1,
          Beschreibung = N'DINT-Lebenszaehler von FALCOM zur CW-SPS. Wird sekündlich hochgezaehlt und dient als Lebenszeichen.'
    WHERE EventID = 301
      AND NodeName = N'Event_301';
END;
