using System.Security.Cryptography;
using Opc.UaFx;
using Opc.UaFx.Client;

namespace Falcom
{
   public sealed class WatchdogSender : BackgroundService
   {
      private static readonly TimeSpan SendInterval = TimeSpan.FromSeconds(1);
      private static readonly TimeSpan ConfigurationRetryDelay = TimeSpan.FromSeconds(5);
      private const string EventName = "Watchdog";
      private const string NodeName = "LebensZaehler";
      private const string Direction = "FALCOM->KRAN_SPS";
      private const string PlaceholderPrefix = "NOCH_ZU_KONFIGURIEREN.";

      private readonly ILogger<WatchdogSender> _logger;
      private readonly ConfigManager _configManager;
      private readonly Parameter _parameter;
      private int counter = RandomNumberGenerator.GetInt32(1, int.MaxValue);
      private string? lastConfigurationIssue;

      public WatchdogSender(
         ILogger<WatchdogSender> logger,
         ConfigManager configManager,
         Parameter parameter)
      {
         _logger = logger;
         _configManager = configManager;
         _parameter = parameter;
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
         _logger.LogInformation(
            "003A|Watchdog-Sender gestartet. Initialer DINT-Lebenszaehler={Counter}.",
            counter);

         while (!stoppingToken.IsCancellationRequested)
         {
            _logger.LogInformation(
               "0043|Watchdog-Versandversuch wird gestartet.");

            WatchdogConfiguration? configuration = LoadConfiguration();

            if (configuration is null)
            {
               await Task.Delay(ConfigurationRetryDelay, stoppingToken);
               continue;
            }

            if (lastConfigurationIssue is not null)
            {
               _logger.LogInformation(
                  "0041|Watchdog-Konfiguration ist jetzt gueltig. Der Versand wird gestartet.");
               lastConfigurationIssue = null;
            }

            try
            {
               await SendUntilConfigurationChangesAsync(
                  configuration,
                  stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
               throw;
            }
            catch (Exception ex)
            {
               _logger.LogError(
                  ex,
                  "003C|Watchdog-Lebenszaehler konnte nicht an die SPS gesendet werden. Neuer Versuch in {DelaySeconds} Sekunden.",
                  ConfigurationRetryDelay.TotalSeconds);
               await Task.Delay(ConfigurationRetryDelay, stoppingToken);
            }
         }
      }

      private async Task SendUntilConfigurationChangesAsync(
         WatchdogConfiguration configuration,
         CancellationToken stoppingToken)
      {
         using var client = new OpcClient(configuration.OpcEndpoint)
         {
            OperationTimeout = 5_000,
            SessionTimeout = 5_000,
            ReconnectTimeout = 5_000
         };

         client.Connect();
         _logger.LogInformation(
            "003D|Watchdog mit SPS verbunden. Endpoint={Endpoint}, Node={Node}.",
            configuration.OpcEndpoint,
            configuration.NodeId);

         using var timer = new PeriodicTimer(SendInterval);
         var configurationCheckCounter = 0;

         while (await timer.WaitForNextTickAsync(stoppingToken))
         {
            counter = GetNextCounter(counter);
            OpcStatus status = client.WriteNode(configuration.NodeId, counter);

            if (status.IsBad)
            {
               throw new InvalidOperationException(
                  $"OPC-Schreiben fehlgeschlagen. Status={status.Code}, Beschreibung={status.Description}");
            }

            _logger.LogDebug(
               "003E|Watchdog LebensZaehler={Counter} an {Node} gesendet.",
               counter,
               configuration.NodeId);

            configurationCheckCounter++;
            if (configurationCheckCounter < 60)
            {
               continue;
            }

            configurationCheckCounter = 0;
            WatchdogConfiguration? currentConfiguration = LoadConfiguration();

            if (currentConfiguration != configuration)
            {
               _logger.LogInformation(
                  "003F|Watchdog-Konfiguration wurde geaendert. OPC-Verbindung wird neu aufgebaut.");
               return;
            }
         }
      }

      private WatchdogConfiguration? LoadConfiguration()
      {
         const string sql = """
            SELECT TOP (1)
               nodes.OPC_Node,
               nodes.DataType,
               events.IsActive
            FROM dbo.FALCOM_EVENTS AS events
            INNER JOIN dbo.FALCOM_EVENT_OPC_NODES AS nodes
               ON nodes.EventID = events.ID
            WHERE events.EventName = N'Watchdog'
              AND events.Direction = N'FALCOM->KRAN_SPS'
              AND nodes.NodeName = N'LebensZaehler'
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
         bool isActive = Convert.ToBoolean(row["IsActive"]);

         if (!isActive)
         {
            LogConfigurationIssue(
               "Event Watchdog ist in FALCOM_EVENTS deaktiviert.");
            return null;
         }

         if (string.IsNullOrWhiteSpace(nodeId)
             || nodeId.StartsWith(PlaceholderPrefix, StringComparison.OrdinalIgnoreCase))
         {
            LogConfigurationIssue(
               $"OPC-Node ist noch nicht konfiguriert. Aktuell='{nodeId}'.");
            return null;
         }

         if (!nodeId.EndsWith(NodeName, StringComparison.OrdinalIgnoreCase))
         {
            LogConfigurationIssue(
               $"OPC-Node muss auf '{NodeName}' enden. Aktuell='{nodeId}'.");
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

      private static int GetNextCounter(int current)
      {
         return current == int.MaxValue
            ? 1
            : current + 1;
      }

      public override async Task StopAsync(CancellationToken cancellationToken)
      {
         _logger.LogInformation(
            "0042|Watchdog-Sender wird beendet.");
         await base.StopAsync(cancellationToken);
      }

      private sealed record WatchdogConfiguration(
         string OpcEndpoint,
         string NodeId);
   }
}
