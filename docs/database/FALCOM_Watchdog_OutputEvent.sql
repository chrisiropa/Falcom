USE [FG]
GO

/*
   Trennt die beiden Lebenszeichen sauber:

   - LebensZaehler / KRAN_SPS->FALCOM:
     Lebenszeichen der Kran-SPS, wird von FALCOM empfangen.

   - Watchdog / FALCOM->KRAN_SPS:
     Lebenszeichen von FALCOM, wird an die Kran-SPS geschrieben.
*/

DECLARE @WatchdogEventID bigint;

SELECT @WatchdogEventID = ID
FROM dbo.FALCOM_EVENTS
WHERE EventName = N'Watchdog'
  AND Direction = N'FALCOM->KRAN_SPS';

IF @WatchdogEventID IS NULL
BEGIN
   INSERT INTO dbo.FALCOM_EVENTS
   (
      EventName,
      Direction,
      Description,
      IsActive
   )
   VALUES
   (
      N'Watchdog',
      N'FALCOM->KRAN_SPS',
      N'Lebenszaehler von FALCOM an die Kran-SPS.',
      1
   );

   SET @WatchdogEventID = CONVERT(bigint, SCOPE_IDENTITY());
END
ELSE
BEGIN
   UPDATE dbo.FALCOM_EVENTS
      SET Description = N'Lebenszaehler von FALCOM an die Kran-SPS.',
          IsActive = 1
    WHERE ID = @WatchdogEventID;
END

IF EXISTS (
   SELECT 1
   FROM dbo.FALCOM_EVENT_OPC_NODES
   WHERE EventID = @WatchdogEventID
     AND NodeName = N'LebensZaehler')
BEGIN
   UPDATE dbo.FALCOM_EVENT_OPC_NODES
      SET NodeRole = N'Trigger',
          OPC_Node = N'ns=1;s=LagerV.DataBlocks.OPC_Daten_ORG.Static.Watchdog',
          DataType = N'Int32',
          IsRequired = 1
    WHERE EventID = @WatchdogEventID
      AND NodeName = N'LebensZaehler';
END
ELSE
BEGIN
   INSERT INTO dbo.FALCOM_EVENT_OPC_NODES
   (
      EventID,
      NodeName,
      OPC_Node,
      NodeRole,
      DataType,
      IsRequired
   )
   VALUES
   (
      @WatchdogEventID,
      N'LebensZaehler',
      N'ns=1;s=LagerV.DataBlocks.OPC_Daten_ORG.Static.Watchdog',
      N'Trigger',
      N'Int32',
      1
   );
END

UPDATE dbo.FALCOM_EVENTS
   SET Description = N'Lebenszaehler von der Kran-SPS an FALCOM.'
 WHERE EventName = N'LebensZaehler'
   AND Direction = N'KRAN_SPS->FALCOM';
GO
