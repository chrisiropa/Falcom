using Opc.UaFx;
using Opc.UaFx.Client;

namespace Falcom
{
   public sealed class WatchdogSender : IDisposable
   {
      private static readonly TimeSpan ConfigurationRetryDelay = TimeSpan.FromSeconds(5);
      private static readonly TimeSpan ConfigurationRefreshInterval = TimeSpan.FromSeconds(60);
      private const string PlaceholderPrefix = "NOCH_ZU_KONFIGURIEREN.";

      private readonly ILogger<WatchdogSender> _logger;
      private readonly ConfigManager _configManager;
      private readonly Parameter _parameter;
      private readonly SemaphoreSlim sendLock = new(1, 1);
      private string? lastConfigurationIssue;
      private WatchdogConfiguration? configuration;
      private OpcClient? client;
      private DateTime nextConfigurationReadUtc = DateTime.MinValue;
      private bool initialValueLogged;

      public WatchdogSender(
         ILogger<WatchdogSender> logger,
         ConfigManager configManager,
         Parameter parameter)
      {
         _logger = logger;
         _configManager = configManager;
         _parameter = parameter;
      }

      public async Task SendAsync(
         int lebensZaehler,
         CancellationToken stoppingToken)
      {
         await sendLock.WaitAsync(stoppingToken);

         try
         {
            if (!initialValueLogged)
            {
               _logger.LogInformation(
                  "003A|Watchdog-Verarbeitung im Dispatcher gestartet. Initialer DINT-Lebenszaehler={LebensZaehler}.",
                  lebensZaehler);
               initialValueLogged = true;
            }

            if (DateTime.UtcNow >= nextConfigurationReadUtc)
            {
               WatchdogConfiguration? newConfiguration = LoadConfiguration();

               if (newConfiguration is null)
               {
                  configuration = null;
                  Disconnect();
                  nextConfigurationReadUtc = DateTime.UtcNow + ConfigurationRetryDelay;
                  return;
               }

               if (configuration is not null && configuration != newConfiguration)
               {
                  _logger.LogInformation(
                     "003F|Watchdog-Konfiguration wurde geaendert. OPC-Verbindung wird neu aufgebaut.");
                  Disconnect();
               }

               configuration = newConfiguration;
               nextConfigurationReadUtc = DateTime.UtcNow + ConfigurationRefreshInterval;

               if (lastConfigurationIssue is not null)
               {
                  _logger.LogInformation(
                     "0041|Watchdog-Konfiguration ist jetzt gueltig. Der Versand wird gestartet.");
                  lastConfigurationIssue = null;
               }
            }

            if (configuration is null)
            {
               return;
            }

            try
            {
               EnsureConnected(configuration);
               OpcStatus status = client!.WriteNode(
                  configuration.NodeId,
                  lebensZaehler);

               if (status.IsBad)
               {
                  throw new InvalidOperationException(
                     $"OPC-Schreiben fehlgeschlagen. Status={status.Code}, Beschreibung={status.Description}");
               }

               _logger.LogDebug(
                  "003E|Watchdog LebensZaehler={Counter} an {Node} gesendet.",
                  lebensZaehler,
                  configuration.NodeId);
            }
            catch (Exception ex)
            {
               _logger.LogError(
                  ex,
                  "003C|Watchdog-Lebenszaehler konnte beim Dispatcher-Aufruf nicht an die SPS gesendet werden.");
               Disconnect();
               nextConfigurationReadUtc = DateTime.UtcNow + ConfigurationRetryDelay;
            }
         }
         finally
         {
            sendLock.Release();
         }
      }

      private void EnsureConnected(WatchdogConfiguration currentConfiguration)
      {
         if (client is not null)
         {
            return;
         }

         client = new OpcClient(currentConfiguration.OpcEndpoint)
         {
            OperationTimeout = 5_000,
            SessionTimeout = 5_000,
            ReconnectTimeout = 5_000
         };

         client.Connect();
         _logger.LogInformation(
            "003D|Watchdog mit SPS verbunden. Endpoint={Endpoint}, Node={Node}.",
            currentConfiguration.OpcEndpoint,
            currentConfiguration.NodeId);
      }

      private WatchdogConfiguration? LoadConfiguration()
      {
         const string sql = """
            SELECT TOP (1)
               nodes.OPC_Node,
               nodes.DataType
            FROM dbo.FALCOM_EVENTS AS events
            INNER JOIN dbo.FALCOM_EVENT_OPC_NODES AS nodes
               ON nodes.EventID = events.ID
            WHERE events.EventName = N'Watchdog'
              AND events.Direction = N'FALCOM->KRAN_SPS'
              AND nodes.IsRequired = 1;
            """;

         SimpleSqlQuery query = new(_configManager.ConnectionString, sql);

         if (query.Exception is not null)
         {
            _logger.LogError(
               query.Exception,
               "0040|Watchdog-Konfiguration konnte nicht aus der Datenbank gelesen werden.");
            return null;
         }

         if (query.QueryResult is null || query.QueryResult.Count == 0)
         {
            LogConfigurationIssue(
               "Event Watchdog oder Detail LebensZaehler wurde nicht gefunden.");
            return null;
         }

         Dictionary<string, object> row = query.QueryResult[0];
         string nodeId = Convert.ToString(row["OPC_Node"])?.Trim() ?? string.Empty;
         string dataType = Convert.ToString(row["DataType"])?.Trim() ?? string.Empty;

         if (string.IsNullOrWhiteSpace(nodeId)
             || nodeId.StartsWith(PlaceholderPrefix, StringComparison.OrdinalIgnoreCase))
         {
            LogConfigurationIssue(
               $"OPC-Node ist noch nicht konfiguriert. Aktuell='{nodeId}'.");
            return null;
         }

         if (!string.Equals(dataType, "Int32", StringComparison.OrdinalIgnoreCase))
         {
            LogConfigurationIssue(
               $"LebensZaehler muss als Int32 konfiguriert sein. Aktuell='{dataType}'.");
            return null;
         }

         if (string.IsNullOrWhiteSpace(_parameter.OpcServer))
         {
            LogConfigurationIssue(
               "FALCOM_PARAMETER.OpcServer ist leer.");
            return null;
         }

         return new WatchdogConfiguration(
            _parameter.OpcServer.Trim(),
            nodeId);
      }

      private void LogConfigurationIssue(string issue)
      {
         if (string.Equals(
            lastConfigurationIssue,
            issue,
            StringComparison.Ordinal))
         {
            return;
         }

         lastConfigurationIssue = issue;
         _logger.LogWarning(
            "003B|Watchdog ist nicht sendebereit: {Reason} Neuer Konfigurationsversuch in {DelaySeconds} Sekunden.",
            issue,
            ConfigurationRetryDelay.TotalSeconds);
      }

      private void Disconnect()
      {
         if (client is null)
         {
            return;
         }

         try
         {
            client.Disconnect();
         }
         finally
         {
            client.Dispose();
            client = null;
         }
      }

      public void Dispose()
      {
         Disconnect();
         sendLock.Dispose();
         _logger.LogInformation(
            "0042|Watchdog-Sender wird beendet.");
      }

      private sealed record WatchdogConfiguration(
         string OpcEndpoint,
         string NodeId);
   }
}
