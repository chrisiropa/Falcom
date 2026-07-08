SET NOCOUNT ON;
GO

CREATE OR ALTER FUNCTION dbo.FALCOM_GetParameterValueFn
(
   @Name nvarchar(256)
)
RETURNS TABLE
AS
RETURN
(
   SELECT TOP (1)
      Name,
      Wert,
      Beschreibung
   FROM dbo.FALCOM_Parameter
   WHERE Name = @Name
);
GO

CREATE OR ALTER PROCEDURE dbo.FALCOM_GetParameterValue
   @Name nvarchar(256)
AS
BEGIN
   SET NOCOUNT ON;

   SELECT
      Name,
      Wert,
      Beschreibung
   FROM dbo.FALCOM_GetParameterValueFn(@Name);
END;
GO

MERGE dbo.FALCOM_Parameter AS target
USING (VALUES
   (N'KundenVisuModernHttpsPort', N'7218', N'HTTPS-Port der KundenVisuModern-Webanwendung / SignalR-Server.'),
   (N'KundenVisuModernHttpPort',  N'5043', N'HTTP-Port der KundenVisuModern-Webanwendung.'),
   (N'KundenVisuModernBindHost',  N'localhost', N'Host/IP fuer Webserver-Bindung. Lokal: localhost, Dienst/Netzwerk ggf. 0.0.0.0 oder konkreter Hostname.'),
   (N'KranLiveHubScheme',         N'https', N'Schema fuer die SignalR-Hub-URL.'),
   (N'KranLiveHubHost',           N'localhost', N'Host fuer die SignalR-Hub-URL aus Sicht des Falcom-Dienstes.'),
   (N'KranLiveHubPath',           N'/falcom-kran-hub', N'Pfad des SignalR-Hubs in KundenVisuModern.')
) AS source (Name, Wert, Beschreibung)
ON target.Name = source.Name
WHEN MATCHED THEN
   UPDATE SET
      target.Wert = source.Wert,
      target.Beschreibung = source.Beschreibung
WHEN NOT MATCHED THEN
   INSERT (Name, Wert, Beschreibung)
   VALUES (source.Name, source.Wert, source.Beschreibung);
GO