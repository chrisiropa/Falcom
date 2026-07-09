using Microsoft.Data.SqlClient;
using Opc.UaFx;
using Opc.UaFx.Client;
using System.Data;

namespace Falcom
{
   public sealed class OPC_Client_Crane : IDisposable
   {
      private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(5);
      private const string KranPositionEventName = "KranPosition";
      private const string KranPositionDirection = "KRAN_SPS->FALCOM";
      private const string PosKranXNodeName = "PosKranX";
      private const string PosKatzeYNodeName = "PosKatzeY";
      private const string PosHubZNodeName = "PosHubZ";
      private const string LebensZaehlerNodeName = "LebensZaehler";

      private readonly ILogger<OPC_Client_Crane> _logger;
      private readonly ConfigManager _configManager;
      private readonly FalcomRuntimeStatus _runtimeStatus;
      private readonly FalcomKranLiveSignalRClient _kranLiveSignalRClient;
      private readonly FalcomEventQueue _eventQueue; // NEU: Privates Feld für die Queue
      private readonly object _syncRoot = new();
      private readonly List<OpcMonitoredItem> monitoredItems = new();
      private readonly string opcServerEndpoint;
      private readonly string falcomWatchdogNodeId;
      private readonly string kranSpsLebensZaehlerNodeId;
      private readonly Dictionary<string, string> kranPositionOpcNodesByName;
      private OpcClient? client = null;
      private OpcSubscription? subscription = null;
      private bool spsDataUnavailable;
      private volatile bool spsLebensZaehlerFreigegeben;
      private DateTime nextDataFlowErrorLogUtc = DateTime.MinValue;
      private DateTime nextKranSpsLebensZaehlerLogUtc = DateTime.UtcNow.AddMinutes(1);
      private DateTime nextKranPositionLogUtc = DateTime.UtcNow.AddMinutes(1);
      private DateTime nextKranPositionConfigurationLogUtc = DateTime.MinValue;
      private int kranfahrtAuftragTelegrammNummer;
      private int kranfahrtAuftragZaehlerAnfahrt;
      private int kranSpsLebensZaehlerEventsInCurrentMinute;
      private int kranPositionEventsInCurrentMinute;
      private int? aktuellePosKranX;
      private int? aktuellePosKatzeY;
      private int? aktuellePosHubZ;
      private string? lastKranfahrtAuftragConfigurationIssue;
      private bool disposed;

      // NEU: FalcomEventQueue im Konstruktor anfordern
      public OPC_Client_Crane(
         ILogger<OPC_Client_Crane> logger,
         Parameter parameter,
         ConfigManager configManager,
         FalcomRuntimeStatus runtimeStatus,
         FalcomKranLiveSignalRClient kranLiveSignalRClient,
         FalcomEventQueue eventQueue)
      {
         _logger = logger;
         _configManager = configManager;
         _runtimeStatus = runtimeStatus;
         _kranLiveSignalRClient = kranLiveSignalRClient;
         _eventQueue = eventQueue; // NEU: Zuweisung für den späteren Zugriff
         TraegerLicense();
         KranfahrtBeendetEvent.LoadOpcNodes(configManager);
         falcomWatchdogNodeId = LoadRequiredEventOpcNode(
            eventName: WatchdogEvent.EventName,
            direction: "FALCOM->KRAN_SPS",
            nodeName: "LebensZaehler");
         kranSpsLebensZaehlerNodeId = LoadRequiredEventOpcNode(
            eventName: KranSpsLebensZaehlerEvent.EventName,
            direction: "KRAN_SPS->FALCOM",
            nodeName: "LebensZaehler");
         kranPositionOpcNodesByName = LoadOptionalEventOpcNodes(
            KranPositionEventName,
            KranPositionDirection);

         if (string.IsNullOrWhiteSpace(parameter.OpcServer))
         {
            throw new InvalidOperationException("Der Datenbankparameter 'OpcServer' ist leer oder wurde nicht gefunden.");
         }

         opcServerEndpoint = parameter.OpcServer.Trim();

         // Client-Instanz das erste Mal erstellen
         CreateClientInstance();

         _logger.LogInformation("0011|OPC_Client_Crane initialisiert fuer {OpcServerEndpoint}. Bereit fuer Connect().", opcServerEndpoint);
      }

      /// <summary>
      /// Hilfsmethode zur sauberen Kapselung der Client-Instanziierung (Option B)
      /// </summary>
      private void CreateClientInstance()
      {
         if (this.client is not null)
         {
            this.client.StateChanged -= OnClientStateChanged;
         }

         this.client = new OpcClient(opcServerEndpoint);
         this.client.ReconnectTimeout = 5000;
         this.client.StateChanged += OnClientStateChanged;
      }

      public void Connect()
      {
         ConnectOnce();
      }

      public async Task ConnectUntilConnectedAsync(CancellationToken cancellationToken)
      {
         var attempt = 1;

         while (true)
         {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
               _logger.LogInformation("0012|OPC-Verbindungsversuch {Attempt} wird gestartet.", attempt);
               spsLebensZaehlerFreigegeben = false;
               _runtimeStatus.SetSpsLebensZaehlerUnavailable("OPC-Datenfluss wird geprueft");
               ConnectOnce();
               ValidateWatchdogRead();
               spsLebensZaehlerFreigegeben = true;
               _logger.LogInformation("0013|OPC-Verbindungsversuch {Attempt} erfolgreich abgeschlossen.", attempt);
               return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
               throw;
            }
            catch (Exception ex)
            {
               _logger.LogError(ex, "0014|OPC-Verbindungsversuch {Attempt} fehlgeschlagen. Naechster Versuch in {DelaySeconds} Sekunden.", attempt, ConnectRetryDelay.TotalSeconds);
               attempt++;
               await Task.Delay(ConnectRetryDelay, cancellationToken);
            }
         }
      }

      public async Task EnsureDataFlowAsync(CancellationToken cancellationToken)
      {
         var attempt = 1;

         while (true)
         {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
               EnsureConnected();
               ValidateWatchdogRead();
               spsLebensZaehlerFreigegeben = true;

               if (spsDataUnavailable)
               {
                  spsDataUnavailable = false;
                  _logger.LogInformation("0015|SPS-Daten sind wieder lesbar. OPC-Subscription is wieder aktiv.");
               }

               return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
               throw;
            }
            catch (Exception ex)
            {
               spsDataUnavailable = true;
               spsLebensZaehlerFreigegeben = false;
               _runtimeStatus.SetSpsLebensZaehlerUnavailable("OPC-Datenfluss nicht freigegeben");

               if (DateTime.UtcNow >= nextDataFlowErrorLogUtc)
               {
                  _logger.LogError(ex, "0016|SPS-Datenpruefung fehlgeschlagen. Wiederherstellungsversuch {Attempt} in {DelaySeconds} Sekunden. Weitere gleiche Datenflussfehler werden fuer 60 Sekunden gedrosselt.", attempt, ConnectRetryDelay.TotalSeconds);
                  nextDataFlowErrorLogUtc = DateTime.UtcNow.AddMinutes(1);
               }

               await Task.Delay(ConnectRetryDelay, cancellationToken);

               try
               {
                  _logger.LogInformation("0017|OPC-Wiederherstellungsversuch {Attempt} wird gestartet (Radikaler Reconnect).", attempt);

                  lock (_syncRoot)
                  {
                     if (client is not null)
                     {
                        try
                        {
                           client.Disconnect();
                        }
                        catch
                        {
                        }
                        client.Dispose();
                        client = null;
                     }

                     CreateClientInstance();
                     ConnectOnce();
                  }

                  _logger.LogInformation("0018|OPC-Wiederherstellungsversuch {Attempt} abgeschlossen. SPS-Daten werden erneut geprueft.", attempt);
               }
               catch (Exception reconnectEx)
               {
                  _logger.LogError(reconnectEx, "0019|OPC-Wiederherstellungsversuch {Attempt} fehlgeschlagen.", attempt);
               }

               attempt++;
            }
         }
      }

      public Task<OpcSendResult> SendKranfahrtAuftragAsync(
         KranfahrtAuftragEvent kranfahrtAuftragEvent,
         CancellationToken cancellationToken)
      {
         cancellationToken.ThrowIfCancellationRequested();

         KranfahrtAuftragOpcNodes? nodes = LoadKranfahrtAuftragOpcNodes();

         if (nodes is null)
         {
            return Task.FromResult(OpcSendResult.Failed(
               lastKranfahrtAuftragConfigurationIssue
               ?? "KranfahrtAuftrag-OPC-Nodes sind nicht gueltig konfiguriert."));
         }

         try
         {
            EnsureConnected();

            int telegrammNummer = kranfahrtAuftragTelegrammNummer == int.MaxValue
               ? 0
               : kranfahrtAuftragTelegrammNummer + 1;
            int zaehlerAnfahrt = kranfahrtAuftragZaehlerAnfahrt == int.MaxValue
               ? 0
               : kranfahrtAuftragZaehlerAnfahrt + 1;

            kranfahrtAuftragEvent.SetZaehlerAnfahrt(zaehlerAnfahrt);

            WriteRequiredNode(nodes.AuftragNummer, Convert.ToInt32(kranfahrtAuftragEvent.AuftragNummer));
            WriteRequiredNode(nodes.AuftragTeilfahrt, kranfahrtAuftragEvent.AuftragTeilfahrt);
            WriteRequiredNodeWithStringFallback(
               nodes.Quelle,
               Convert.ToInt32(kranfahrtAuftragEvent.QuellePositionID),
               KranfahrtAuftragEvent.QuelleNodeName);
            WriteRequiredNodeWithStringFallback(
               nodes.Ziel,
               Convert.ToInt32(kranfahrtAuftragEvent.ZielPositionID),
               KranfahrtAuftragEvent.ZielNodeName);
            WriteRequiredNode(nodes.SollMasse, Convert.ToDouble(kranfahrtAuftragEvent.SollMasseKg));
            WriteRequiredNode(nodes.Toleranz, Convert.ToDouble(kranfahrtAuftragEvent.ToleranzKg));
            WriteRequiredNode(nodes.TelegrammNummer, telegrammNummer);
            WriteRequiredNode(nodes.ZaehlerAnfahrt, kranfahrtAuftragEvent.ZaehlerAnfahrt);

            kranfahrtAuftragTelegrammNummer = telegrammNummer;
            kranfahrtAuftragZaehlerAnfahrt = zaehlerAnfahrt;

            _logger.LogInformation(
               "0047|KranfahrtAuftrag an SPS gesendet: Auftrag={AuftragID}, Teilfahrt={AuftragTeilfahrt}, QuellePositionID={QuellePositionID}, ZielPositionID={ZielPositionID}, SollMasseKg={SollMasseKg}, ToleranzKg={ToleranzKg}, TelegrammNummer={TelegrammNummer}, ZaehlerAnfahrt={ZaehlerAnfahrt}.",
               kranfahrtAuftragEvent.AuftragNummer,
               kranfahrtAuftragEvent.AuftragTeilfahrt,
               kranfahrtAuftragEvent.QuellePositionID,
               kranfahrtAuftragEvent.ZielPositionID,
               kranfahrtAuftragEvent.SollMasseKg,
               kranfahrtAuftragEvent.ToleranzKg,
               telegrammNummer,
               kranfahrtAuftragEvent.ZaehlerAnfahrt);

            return Task.FromResult(OpcSendResult.Ok());
         }
         catch (Exception ex)
         {
            _logger.LogError(
               ex,
               "0048|KranfahrtAuftrag konnte nicht an die SPS gesendet werden. Auftrag={AuftragID}, QuellePositionID={QuellePositionID}, ZielPositionID={ZielPositionID}.",
               kranfahrtAuftragEvent.AuftragNummer,
               kranfahrtAuftragEvent.QuellePositionID,
               kranfahrtAuftragEvent.ZielPositionID);
            return Task.FromResult(OpcSendResult.Failed(
               $"KranfahrtAuftrag konnte nicht an die SPS gesendet werden: {ex.Message}"));
         }
      }

      private void EnsureConnected()
      {
         lock (_syncRoot)
         {
            if (client is { State: OpcClientState.Connected })
            {
               return;
            }

            _logger.LogInformation(
               "0049|OPC-Kranclient ist nicht verbunden. Verbindung wird vor der SPS-Operation aufgebaut.");
            ConnectOnce();
         }
      }

      private KranfahrtAuftragOpcNodes? LoadKranfahrtAuftragOpcNodes()
      {
         Dictionary<string, string> opcNodes = new(StringComparer.OrdinalIgnoreCase);

         try
         {
            using SqlConnection connection = new(_configManager.ConnectionString);
            using SqlCommand command = new("dbo.FALCOM_GetEventOpcNodes", connection)
            {
               CommandType = CommandType.StoredProcedure,
               CommandTimeout = 30
            };

            command.Parameters.Add("@EventName", SqlDbType.NVarChar, 128).Value =
               KranfahrtAuftragEvent.EventName;
            command.Parameters.Add("@Direction", SqlDbType.NVarChar, 64).Value =
               KranfahrtAuftragEvent.Direction;

            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
               string nodeName = Convert.ToString(reader["NodeName"]) ?? string.Empty;
               string opcNode = Convert.ToString(reader["OPC_Node"]) ?? string.Empty;

               if (!string.IsNullOrWhiteSpace(nodeName))
               {
                  opcNodes[nodeName] = opcNode;
               }
            }
         }
         catch (Exception ex)
         {
            _logger.LogError(
               ex,
               "004A|OPC-Nodes fuer KranfahrtAuftrag konnten nicht aus der Datenbank gelesen werden.");
            return null;
         }

         string[] requiredNodeNames =
         [
            KranfahrtAuftragEvent.AuftragNummerNodeName,
            KranfahrtAuftragEvent.AuftragTeilfahrtNodeName,
            KranfahrtAuftragEvent.QuelleNodeName,
            KranfahrtAuftragEvent.ZielNodeName,
            KranfahrtAuftragEvent.SollMasseNodeName,
            KranfahrtAuftragEvent.ToleranzNodeName,
            KranfahrtAuftragEvent.TelegrammNummerNodeName,
            KranfahrtAuftragEvent.ZaehlerAnfahrtNodeName
         ];

         foreach (string nodeName in requiredNodeNames)
         {
            if (!opcNodes.TryGetValue(nodeName, out string? opcNode)
                || string.IsNullOrWhiteSpace(opcNode)
                || opcNode.StartsWith("NOCH_ZU_KONFIGURIEREN.", StringComparison.OrdinalIgnoreCase)
                || string.Equals(opcNode, "Trigger Richtung SPS, einfach hochzählen", StringComparison.OrdinalIgnoreCase))
            {
               LogKranfahrtAuftragConfigurationIssue(
                  $"Node '{nodeName}' ist noch nicht gueltig konfiguriert.");
               return null;
            }
         }

         return new KranfahrtAuftragOpcNodes(
            opcNodes[KranfahrtAuftragEvent.AuftragNummerNodeName].Trim(),
            opcNodes[KranfahrtAuftragEvent.AuftragTeilfahrtNodeName].Trim(),
            opcNodes[KranfahrtAuftragEvent.QuelleNodeName].Trim(),
            opcNodes[KranfahrtAuftragEvent.ZielNodeName].Trim(),
            opcNodes[KranfahrtAuftragEvent.SollMasseNodeName].Trim(),
            opcNodes[KranfahrtAuftragEvent.ToleranzNodeName].Trim(),
            opcNodes[KranfahrtAuftragEvent.TelegrammNummerNodeName].Trim(),
            opcNodes[KranfahrtAuftragEvent.ZaehlerAnfahrtNodeName].Trim());
      }

      private Dictionary<string, string> LoadOptionalEventOpcNodes(
         string eventName,
         string direction)
      {
         var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

         try
         {
            using SqlConnection connection = new(_configManager.ConnectionString);
            using SqlCommand command = new("dbo.FALCOM_GetEventOpcNodes", connection)
            {
               CommandType = CommandType.StoredProcedure,
               CommandTimeout = 30
            };

            command.Parameters.Add("@EventName", SqlDbType.NVarChar, 128).Value = eventName;
            command.Parameters.Add("@Direction", SqlDbType.NVarChar, 64).Value = direction;

            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
               string nodeName = Convert.ToString(reader["NodeName"])?.Trim() ?? string.Empty;
               string opcNode = Convert.ToString(reader["OPC_Node"])?.Trim() ?? string.Empty;

               if (string.IsNullOrWhiteSpace(nodeName))
               {
                  continue;
               }

               result[nodeName] = opcNode;
            }
         }
         catch (Exception ex)
         {
            _logger.LogWarning(
               ex,
               "0058|Optionale OPC-Event-Konfiguration konnte nicht geladen werden. Event={EventName}, Direction={Direction}.",
               eventName,
               direction);
         }

         return result;
      }

      private static bool IsConfiguredOpcNode(string? opcNode)
      {
         return !string.IsNullOrWhiteSpace(opcNode)
                && !opcNode.StartsWith("NOCH_ZU_KONFIGURIEREN.", StringComparison.OrdinalIgnoreCase);
      }
      private string LoadRequiredEventOpcNode(
         string eventName,
         string direction,
         string nodeName)
      {
         try
         {
            using SqlConnection connection = new(_configManager.ConnectionString);
            using SqlCommand command = new("dbo.FALCOM_GetEventOpcNodes", connection)
            {
               CommandType = CommandType.StoredProcedure,
               CommandTimeout = 30
            };

            command.Parameters.Add("@EventName", SqlDbType.NVarChar, 128).Value = eventName;
            command.Parameters.Add("@Direction", SqlDbType.NVarChar, 64).Value = direction;

            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();

            while (reader.Read())
            {
               string configuredNodeName = Convert.ToString(reader["NodeName"])?.Trim() ?? string.Empty;

               if (!string.Equals(configuredNodeName, nodeName, StringComparison.OrdinalIgnoreCase))
               {
                  continue;
               }

               string opcNode = Convert.ToString(reader["OPC_Node"])?.Trim() ?? string.Empty;

               if (string.IsNullOrWhiteSpace(opcNode)
                   || opcNode.StartsWith("NOCH_ZU_KONFIGURIEREN.", StringComparison.OrdinalIgnoreCase))
               {
                  throw new InvalidOperationException(
                     $"OPC-Node fuer {eventName}.{nodeName} ist nicht gueltig konfiguriert. Aktuell='{opcNode}'.");
               }

               return opcNode;
            }
         }
         catch (Exception ex) when (ex is not InvalidOperationException)
         {
            throw new InvalidOperationException(
               $"OPC-Node fuer {eventName}.{nodeName} konnte nicht aus der Datenbank gelesen werden.",
               ex);
         }

         throw new InvalidOperationException(
            $"OPC-Node fuer {eventName}.{nodeName} Richtung {direction} wurde nicht gefunden.");
      }

      private void LogKranfahrtAuftragConfigurationIssue(string reason)
      {
         lastKranfahrtAuftragConfigurationIssue = reason;

         _logger.LogWarning(
            "004B|KranfahrtAuftrag wird noch nicht an die SPS gesendet: {Reason}",
            reason);
      }

      private void WriteRequiredNodeWithStringFallback(
         string nodeId,
         int value,
         string nodeName)
      {
         try
         {
            WriteRequiredNode(nodeId, value);
         }
         catch (Exception ex)
         {
            _logger.LogWarning(
               "0053|OPC Senden: Node {NodeName} akzeptiert Int32 aktuell nicht. Sende denselben Wert tolerant als String. Node={Node}, Wert={Value}. Grund={Reason}",
               nodeName,
               nodeId,
               value,
               ex.Message);

            WriteRequiredNode(
               nodeId,
               value.ToString(System.Globalization.CultureInfo.InvariantCulture));
         }
      }

      private void WriteRequiredNode(string nodeId, object value)
      {
         _logger.LogInformation(
            "0051|OPC Senden: Node={Node}, Wert={Value}",
            nodeId,
            value);
         OpcStatus status = client!.WriteNode(nodeId, value);

         if (status.IsBad)
         {
            throw new InvalidOperationException(
               $"OPC-Schreiben fehlgeschlagen. Node={nodeId}, Status={status.Code}, Beschreibung={status.Description}");
         }
      }

      private void ConnectOnce()
      {
         if (disposed)
         {
            throw new ObjectDisposedException(nameof(OPC_Client_Crane));
         }

         lock (_syncRoot)
         {
            ResetSubscription();

            _logger.LogInformation("001A|Verbindung zu {OpcServerEndpoint} wird aufgebaut.", opcServerEndpoint);
            client?.Connect();

            subscription = client?.SubscribeNodes();

            if (!ConnectChannels())
            {
               throw new InvalidOperationException("OPC-Kanal 'Zaehler' konnte nicht registriert werden.");
            }

            _logger.LogInformation("001B|OPC-Verbindung und Kanalregistrierung sind bereit.");
         }
      }

      private sealed record KranfahrtAuftragOpcNodes(
         string AuftragNummer,
         string AuftragTeilfahrt,
         string Quelle,
         string Ziel,
         string SollMasse,
         string Toleranz,
         string TelegrammNummer,
         string ZaehlerAnfahrt);

      public sealed record OpcSendResult(bool Success, string Reason)
      {
         public static OpcSendResult Ok()
         {
            return new OpcSendResult(true, string.Empty);
         }

         public static OpcSendResult Failed(string reason)
         {
            return new OpcSendResult(false, reason);
         }
      }

      private void ValidateWatchdogRead()
      {
         if (disposed)
         {
            throw new ObjectDisposedException(nameof(OPC_Client_Crane));
         }

         lock (_syncRoot)
         {
            if (client is null)
            {
               throw new InvalidOperationException(
                  $"OPC-Client ist nicht initialisiert. Watchdog-Node konnte nicht gelesen werden. Node={falcomWatchdogNodeId}");
            }

            OpcValue watchdogValue;

            try
            {
               watchdogValue = client.ReadNode(falcomWatchdogNodeId);
            }
            catch (Exception ex)
            {
               throw new InvalidOperationException(
                  $"Watchdog-Node konnte nicht gelesen werden. Node={falcomWatchdogNodeId}",
                  ex);
            }

            if (!watchdogValue.Status.IsGood)
            {
               throw new InvalidOperationException(
                  $"Watchdog-Node ist nicht sauber lesbar. Node={falcomWatchdogNodeId}, Status={watchdogValue.Status.Code}, Wert={watchdogValue.Value}");
            }

            _logger.LogDebug(
               "001C|Watchdog-Node erfolgreich gelesen. Node={Node}, Status={Status}, Wert={Value}",
               falcomWatchdogNodeId,
               watchdogValue.Status.Code,
               watchdogValue.Value);
         }
      }

      public void Disconnect()
      {
         lock (_syncRoot)
         {
            if (disposed)
            {
               return;
            }

            try
            {
               _logger.LogInformation("001D|OPC_Client_Crane wird heruntergefahren.");

               ResetSubscription();

               if (client is not null)
               {
                  client.StateChanged -= OnClientStateChanged;
                  client.Disconnect();
               }

               _logger.LogInformation("001E|OPC_Client_Crane wurde getrennt.");
            }
            catch (Exception ex)
            {
               _logger.LogError(ex, "001F|Fehler beim Herunterfahren des OPC_Client_Crane.");
            }
         }
      }

      public void Dispose()
      {
         lock (_syncRoot)
         {
            if (disposed)
            {
               return;
            }

            if (client is not null)
            {
               client.StateChanged -= OnClientStateChanged;
               client.Dispose();
               client = null;
            }
            disposed = true;
         }
      }

      private void ResetSubscription()
      {
         if (subscription is null)
         {
            monitoredItems.Clear();
            return;
         }

         foreach (OpcMonitoredItem item in monitoredItems)
         {
            item.DataChangeReceived -= HandleDataChange;
         }

         if (monitoredItems.Count > 0)
         {
            try
            {
               subscription.RemoveMonitoredItem(monitoredItems);
               subscription.ApplyChanges();
            }
            catch (Exception ex)
            {
               _logger.LogDebug(ex, "0020|Alte Subscription konnte wegen totem Kanal nicht sauber entfernt werden. Wird erzwungen.");
            }

            monitoredItems.Clear();
         }

         subscription = null;
      }

      public Boolean ConnectChannels()
      {
         if (subscription == null || client == null) return false;

         var zaehlerItem = new OpcMonitoredItem(kranSpsLebensZaehlerNodeId, OpcAttribute.Value);
         zaehlerItem.DataChangeReceived += HandleDataChange;
         subscription.AddMonitoredItem(zaehlerItem);
         monitoredItems.Add(zaehlerItem);

         var kranfahrtBeendetItem = new OpcMonitoredItem(KranfahrtBeendetEvent.ÄnderungsZaehlerOPCNode, OpcAttribute.Value);
         kranfahrtBeendetItem.DataChangeReceived += HandleDataChange;
         subscription.AddMonitoredItem(kranfahrtBeendetItem);
         monitoredItems.Add(kranfahrtBeendetItem);

         if (kranPositionOpcNodesByName.TryGetValue(LebensZaehlerNodeName, out string? kranPositionTriggerNode)
             && IsConfiguredOpcNode(kranPositionTriggerNode)
             && !string.Equals(kranPositionTriggerNode, kranSpsLebensZaehlerNodeId, StringComparison.Ordinal))
         {
            var positionTriggerItem = new OpcMonitoredItem(kranPositionTriggerNode, OpcAttribute.Value)
            {
               Tag = "KranPosition.LebensZaehler"
            };
            positionTriggerItem.DataChangeReceived += HandleDataChange;
            subscription.AddMonitoredItem(positionTriggerItem);
            monitoredItems.Add(positionTriggerItem);
         }

         subscription.ApplyChanges();

         _logger.LogInformation("0021|Kanal 'Zähler' erfolgreich registriert.");
         return true;
      }

      public Boolean ConnectChannels_WD_TEST()
      {
         if (subscription == null || client == null) return false;

         var item = new OpcMonitoredItem(falcomWatchdogNodeId, OpcAttribute.Value)
         {
            Tag = "craneEvent"
         };
         item.DataChangeReceived += HandleDataChange;

         subscription.AddMonitoredItem(item);
         monitoredItems.Add(item);
         subscription.ApplyChanges();

         _logger.LogInformation("0022|Kanal 'Watchdog' erfolgreich registriert.");
         return true;
      }

      private void LogKranSpsLebensZaehlerSummaryIfDue(int currentLebensZaehler)
      {
         DateTime nowUtc = DateTime.UtcNow;

         if (nowUtc < nextKranSpsLebensZaehlerLogUtc)
         {
            return;
         }

         _logger.LogInformation(
            "004E|Kran-SPS LebensZaehler aktiv. In den letzten 60 Sekunden wurden {EventCount} LebensZaehler-Events empfangen. Aktueller LebensZaehler={LebensZaehler}.",
            kranSpsLebensZaehlerEventsInCurrentMinute,
            currentLebensZaehler);

         kranSpsLebensZaehlerEventsInCurrentMinute = 0;
         nextKranSpsLebensZaehlerLogUtc = nowUtc.AddMinutes(1);
      }

      private bool IsKranPositionTriggerNode(string nodeId)
      {
         return kranPositionOpcNodesByName.TryGetValue(LebensZaehlerNodeName, out string? triggerNode)
                && IsConfiguredOpcNode(triggerNode)
                && string.Equals(nodeId, triggerNode, StringComparison.Ordinal);
      }

      private void TryReadAndSendKranPositionFromTrigger(string triggerNodeId)
      {
         if (!IsKranPositionTriggerNode(triggerNodeId))
         {
            return;
         }

         if (!TryGetConfiguredKranPositionNode(PosKranXNodeName, out string posKranXNode)
             || !TryGetConfiguredKranPositionNode(PosKatzeYNodeName, out string posKatzeYNode)
             || !TryGetConfiguredKranPositionNode(PosHubZNodeName, out string posHubZNode))
         {
            LogKranPositionConfigurationIssueIfDue(
               "KranPosition-Trigger empfangen, aber mindestens ein Payload-Node PosKranX/PosKatzeY/PosHubZ ist nicht gueltig konfiguriert.");
            return;
         }

         try
         {
            int posKranX;
            int posKatzeY;
            int posHubZ;

            lock (_syncRoot)
            {
               posKranX = ReadKranPositionPayloadInt32(posKranXNode, PosKranXNodeName);
               posKatzeY = ReadKranPositionPayloadInt32(posKatzeYNode, PosKatzeYNodeName);
               posHubZ = ReadKranPositionPayloadInt32(posHubZNode, PosHubZNodeName);
            }

            aktuellePosKranX = posKranX;
            aktuellePosKatzeY = posKatzeY;
            aktuellePosHubZ = posHubZ;
            kranPositionEventsInCurrentMinute++;

            _ = _kranLiveSignalRClient.SendKranPositionAsync(
               aktuellePosKranX,
               aktuellePosKatzeY,
               aktuellePosHubZ,
               CancellationToken.None);

            LogKranPositionSummaryIfDue();
         }
         catch (Exception ex)
         {
            _logger.LogWarning(
               ex,
               "005C|KranPosition wurde durch LebensZaehler getriggert, aber die Payloads konnten nicht gelesen werden. TriggerNode={TriggerNode}.",
               triggerNodeId);
         }
      }

      private bool TryGetConfiguredKranPositionNode(string nodeName, out string opcNode)
      {
         opcNode = string.Empty;

         if (!kranPositionOpcNodesByName.TryGetValue(nodeName, out string? configuredNode)
             || !IsConfiguredOpcNode(configuredNode))
         {
            return false;
         }

         opcNode = configuredNode;
         return true;
      }

      private int ReadKranPositionPayloadInt32(string opcNode, string nodeName)
      {
         if (client is null)
         {
            throw new InvalidOperationException($"OPC-Client ist nicht initialisiert. KranPosition.{nodeName} konnte nicht gelesen werden. Node={opcNode}");
         }

         OpcValue value = client.ReadNode(opcNode);

         if (!value.Status.IsGood)
         {
            throw new InvalidOperationException($"KranPosition.{nodeName} ist nicht sauber lesbar. Node={opcNode}, Status={value.Status.Code}, Wert={value.Value}");
         }

         return Convert.ToInt32(value.Value);
      }

      private void LogKranPositionConfigurationIssueIfDue(string reason)
      {
         DateTime nowUtc = DateTime.UtcNow;

         if (nowUtc < nextKranPositionConfigurationLogUtc)
         {
            return;
         }

         _logger.LogWarning(
            "005B|KranPosition-Konfiguration ist nicht sendebereit: {Reason}",
            reason);

         nextKranPositionConfigurationLogUtc = nowUtc.AddMinutes(1);
      }

      private void LogKranPositionSummaryIfDue()
      {
         DateTime nowUtc = DateTime.UtcNow;

         if (nowUtc < nextKranPositionLogUtc)
         {
            return;
         }

         _logger.LogInformation(
            "005A|Kranposition aktiv. In den letzten 60 Sekunden wurden {EventCount} Positionswerte empfangen. X={PosKranX}, Y={PosKatzeY}, Z={PosHubZ}.",
            kranPositionEventsInCurrentMinute,
            aktuellePosKranX,
            aktuellePosKatzeY,
            aktuellePosHubZ);

         kranPositionEventsInCurrentMinute = 0;
         nextKranPositionLogUtc = nowUtc.AddMinutes(1);
      }
      private void HandleDataChange(object sender, OpcDataChangeReceivedEventArgs e)
      {
         if (client == null) return;

         try
         {
            var neuerZaehlerWert = e.Item.Value.Value;
            string changedNodeId = e.MonitoredItem.NodeId.ToString();
            _logger.LogInformation(
               "0050|OPC Empfang: Node={Node}, Wert={Value}",
               e.MonitoredItem.NodeId,
               neuerZaehlerWert);
if (string.Equals(
               e.MonitoredItem.NodeId.ToString(),
               kranSpsLebensZaehlerNodeId,
               StringComparison.Ordinal))
            {
               int lebensZaehler = Convert.ToInt32(neuerZaehlerWert);

               if (!spsLebensZaehlerFreigegeben)
               {
                  _logger.LogDebug(
                     "0057|SPS-LebensZaehler empfangen, aber noch nicht freigegeben. Node={Node}, Wert={Value}",
                     e.MonitoredItem.NodeId,
                     lebensZaehler);
                  return;
               }

               var lebensZaehlerEvent = new KranSpsLebensZaehlerEvent(lebensZaehler);

               if (!_eventQueue.Writer.TryWrite(lebensZaehlerEvent))
               {
                  _logger.LogError("004F|KranSpsLebensZaehlerEvent konnte nicht in die Event-Queue geschrieben werden.");
                  return;
               }

               kranSpsLebensZaehlerEventsInCurrentMinute++;
               _runtimeStatus.SetSpsLebensZaehlerReceived(lebensZaehler);
               _ = _kranLiveSignalRClient.SendSpsLebensZaehlerAsync(
                  lebensZaehler,
                  CancellationToken.None);
               LogKranSpsLebensZaehlerSummaryIfDue(lebensZaehler);
               TryReadAndSendKranPositionFromTrigger(changedNodeId);
               return;
            }
            if (IsKranPositionTriggerNode(changedNodeId))
            {
               TryReadAndSendKranPositionFromTrigger(changedNodeId);
               return;
            }

            if (e.MonitoredItem.NodeId.ToString().Contains("ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Zaehler"))
            {
               /*
               //TEST
               string baseNode = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.";

               var cmdStart = client.ReadNode($"{baseNode}Comands.Start")?.Value;
               var cmdStop = client.ReadNode($"{baseNode}Comands.Stop")?.Value;
               var statusRun = client.ReadNode($"{baseNode}Status.Run")?.Value;

               _logger.LogInformation("0024|Werte erfolgreich nachgelesen - Start: {Start}, Stop: {Stop}, Run: {Run}", cmdStart, cmdStop, statusRun);

               if (statusRun is bool isRunning && isRunning)
               {
                  // Deine Kran-Logik
               }
               */
            }
            else if (string.Equals(
               e.MonitoredItem.NodeId.ToString(),
               KranfahrtBeendetEvent.ÄnderungsZaehlerOPCNode,
               StringComparison.Ordinal))
            {
               if (e.Item.Value.Value is not bool isStopped || !isStopped)
               {
                  _logger.LogDebug("0025|Stop-Signal wurde zurueckgesetzt.");
                  return;
               }

               _logger.LogInformation("0026|Kranfahrt beendet");
               int auftragId = Convert.ToInt32(client.ReadNode(KranfahrtBeendetEvent.AuftragsNummerOPCNode).Value);
               int teilfahrtID = Convert.ToInt32(client.ReadNode(KranfahrtBeendetEvent.TeilfahrtIDOPCNode).Value);
               string kranQuelle = Convert.ToString(client.ReadNode(KranfahrtBeendetEvent.KranQuelleOPCNode).Value) ?? string.Empty;
               string kranZiel = Convert.ToString(client.ReadNode(KranfahrtBeendetEvent.KranZielOPCNode).Value) ?? string.Empty;
               double toleranz = Convert.ToDouble(client.ReadNode(KranfahrtBeendetEvent.ToleranzOPCNode).Value);
               double istGewicht = Convert.ToDouble(client.ReadNode(KranfahrtBeendetEvent.IstGewichtOPCNode).Value);
               int fehlercode = Convert.ToInt32(client.ReadNode(KranfahrtBeendetEvent.FehlercodeOPCNode).Value);

               var kranEvent = new KranfahrtBeendetEvent(
                    auftragsNummer: auftragId,
                    teilfahrtID: teilfahrtID,
                    kranQuelle: kranQuelle,
                    kranZiel: kranZiel,
                    toleranz: toleranz,
                    istGewicht: istGewicht,
                    fehlercode: fehlercode,
                    änderungsZähler: Convert.ToInt32(e.Item.Value.Value)
               );

               if (!_eventQueue.Writer.TryWrite(kranEvent))
               {
                  _logger.LogError("0027|KranfahrtBeendetEvent konnte nicht in die Event-Queue geschrieben werden.");
                  return;
               }

               _logger.LogInformation(
                  "0028|KranfahrtBeendetEvent eingereiht: Auftrag={AuftragId}, Quelle={Quelle}, Ziel={Ziel}, Toleranz={Toleranz}, IstGewicht={IstGewicht}, Fehlercode={Fehlercode}",
                  auftragId, kranQuelle, kranZiel, toleranz, istGewicht, fehlercode);
            }
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, "0029|Fehler im HandleDataChange beim Nachlesen der SPS-Daten.");
         }
      }

      #region Verbindungs-Event-Handler

      private void OnClientStateChanged(object? sender, OpcClientStateChangedEventArgs e)
      {
         _logger.LogDebug("002A|OPC Client Zustand geändert von {OldState} zu {NewState}", e.OldState, e.NewState);

         if (e.NewState == OpcClientState.Connected)
         {
            _runtimeStatus.SetOpcKranSpsStatus(true, "Verbunden");
            _logger.LogWarning("002B|OPC UA Client erfolgreich verbunden / wiederverbunden!");
         }
         else if (e.NewState == OpcClientState.Disconnected)
         {
            spsLebensZaehlerFreigegeben = false;
            _runtimeStatus.SetOpcKranSpsStatus(false, "Getrennt");
            _runtimeStatus.SetSpsLebensZaehlerUnavailable("OPC getrennt");
            _logger.LogError("002C|Die Verbindung zum OPC UA Server wurde getrennt!");
         }
         else if (e.NewState == OpcClientState.Reconnecting)
         {
            spsLebensZaehlerFreigegeben = false;
            _runtimeStatus.SetOpcKranSpsStatus(false, "Reconnect laeuft");
            _runtimeStatus.SetSpsLebensZaehlerUnavailable("OPC Reconnect laeuft");
            _logger.LogInformation("002D|Verbindung verloren. Auto-Reconnect versucht gerade die Wiederverbindung...");
         }
      }

      #endregion

      static void TraegerLicense()
      {
         Opc.UaFx.Client.Licenser.LicenseKey = ConfigManager.TraegerLicenseKey;
         var license = Opc.UaFx.Client.Licenser.LicenseInfo;
         Console.WriteLine("Traeger Licence Info = {0} | Gültig = {1}", license.ToString(), !license.IsEvaluation);
      }
   }
}
