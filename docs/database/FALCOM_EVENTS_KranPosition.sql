SET NOCOUNT ON;
GO

DECLARE @EventID bigint;
DECLARE @LebensZaehlerNode nvarchar(1024);

SELECT @EventID = ID
FROM dbo.FALCOM_EVENTS
WHERE EventName = N'KranPosition'
  AND Direction = N'KRAN_SPS->FALCOM';

SELECT @LebensZaehlerNode = nodes.OPC_Node
FROM dbo.FALCOM_EVENTS AS events
INNER JOIN dbo.FALCOM_EVENT_OPC_NODES AS nodes
   ON nodes.EventID = events.ID
WHERE events.EventName = N'LebensZaehlerKran'
  AND events.Direction = N'KRAN_SPS->FALCOM'
  AND nodes.NodeName = N'LebensZaehler';

IF @LebensZaehlerNode IS NULL OR LTRIM(RTRIM(@LebensZaehlerNode)) = N''
BEGIN
   SET @LebensZaehlerNode = N'NOCH_ZU_KONFIGURIEREN.LebensZaehler';
END;

IF @EventID IS NULL
BEGIN
   INSERT INTO dbo.FALCOM_EVENTS
      (EventName, Direction, Description, IsActive)
   VALUES
      (N'KranPosition', N'KRAN_SPS->FALCOM', N'Live-Position des Krans inklusive Katze und Hub.', 1);

   SET @EventID = CONVERT(bigint, SCOPE_IDENTITY());
END
ELSE
BEGIN
   UPDATE dbo.FALCOM_EVENTS
      SET Description = N'Live-Position des Krans inklusive Katze und Hub.',
          IsActive = 1
    WHERE ID = @EventID;
END;

MERGE dbo.FALCOM_EVENT_OPC_NODES AS target
USING (VALUES
   (N'LebensZaehler', @LebensZaehlerNode,                    N'Trigger', N'Int32'),
   (N'PosKranX',      N'NOCH_ZU_KONFIGURIEREN.PosKranX',     N'Payload', N'Int32'),
   (N'PosKatzeY',     N'NOCH_ZU_KONFIGURIEREN.PosKatzeY',    N'Payload', N'Int32'),
   (N'PosHubZ',       N'NOCH_ZU_KONFIGURIEREN.PosHubZ',      N'Payload', N'Int32')
) AS source (NodeName, OPC_Node, NodeRole, DataType)
ON target.EventID = @EventID
AND target.NodeName = source.NodeName
WHEN MATCHED THEN
   UPDATE SET
      target.OPC_Node = CASE
                           WHEN source.NodeRole = N'Trigger' THEN source.OPC_Node
                           ELSE target.OPC_Node
                        END,
      target.NodeRole = source.NodeRole,
      target.DataType = source.DataType,
      target.IsRequired = 1
WHEN NOT MATCHED THEN
   INSERT (EventID, NodeName, OPC_Node, NodeRole, DataType, IsRequired)
   VALUES (@EventID, source.NodeName, source.OPC_Node, source.NodeRole, source.DataType, 1);
GO