using Microsoft.Data.SqlClient;
using Opc.UaFx;
using Opc.UaFx.Client;
using System.Data;

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
      private readonly FalcomRuntimeStatus _runtimeStatus;
      private readonly SemaphoreSlim sendLock = new(1, 1);
      private string? lastConfigurationIssue;
      private WatchdogConfiguration? configuration;
      private OpcClient? client;
      private DateTime nextConfigurationReadUtc = DateTime.MinValue;
      private DateTime nextSendErrorLogUtc = DateTime.MinValue;
      private bool initialValueLogged;

      public WatchdogSender(
         ILogger<WatchdogSender> logger,
         ConfigManager configManager,
         Parameter parameter,
         FalcomRuntimeStatus runtimeStatus)
      {
         _logger = logger;
         _configManager = configManager;
         _parameter = parameter;
         _runtimeStatus = runtimeStatus;
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

               nextSendErrorLogUtc = DateTime.MinValue;
               _runtimeStatus.SetWatchdogSent(lebensZaehler);

               _logger.LogDebug(
                  "003E|Watchdog LebensZaehler={Counter} an {Node} gesendet.",
                  lebensZaehler,
                  configuration.NodeId);
            }
            catch (Exception ex)
            {
               if (DateTime.UtcNow >= nextSendErrorLogUtc)
               {
                  _logger.LogError(
                     ex,
                     "003C|Watchdog-Lebenszaehler konnte beim Dispatcher-Aufruf nicht an die SPS gesendet werden. Weitere gleiche Sendefehler werden fuer 60 Sekunden gedrosselt.");
                  nextSendErrorLogUtc = DateTime.UtcNow + ConfigurationRefreshInterval;
               }

               _runtimeStatus.SetWatchdogError("Sendefehler");
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
         string nodeId = string.Empty;
         string dataType = string.Empty;

         try
         {
            using SqlConnection connection = new(_configManager.ConnectionString);
            using SqlCommand command = new("dbo.FALCOM_GetEventOpcNodes", connection)
            {
               CommandType = CommandType.StoredProcedure,
               CommandTimeout = 30
            };

            command.Parameters.Add("@EventName", SqlDbType.NVarChar, 128).Value = "Watchdog";
            command.Parameters.Add("@Direction", SqlDbType.NVarChar, 64).Value = "FALCOM->KRAN_SPS";

            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
               string nodeName = Convert.ToString(reader["NodeName"])?.Trim() ?? string.Empty;

               if (!string.Equals(nodeName, "LebensZaehler", StringComparison.OrdinalIgnoreCase))
               {
                  continue;
               }

               nodeId = Convert.ToString(reader["OPC_Node"])?.Trim() ?? string.Empty;
               dataType = Convert.ToString(reader["DataType"])?.Trim() ?? string.Empty;
               break;
            }
         }
         catch (Exception ex)
         {
            _logger.LogError(
               ex,
               "0040|Watchdog-Konfiguration konnte nicht aus der Datenbank gelesen werden.");
            return null;
         }

         if (string.IsNullOrWhiteSpace(nodeId))
         {
            LogConfigurationIssue(
               "Event Watchdog Richtung FALCOM->KRAN_SPS oder Detail LebensZaehler wurde nicht gefunden.");
            return null;
         }

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
