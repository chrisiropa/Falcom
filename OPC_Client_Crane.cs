using Opc.UaFx;
using Opc.UaFx.Client;

namespace Falcom
{
   public sealed class OPC_Client_Crane : IDisposable
   {
      private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(5);
      private const string ZaehlerNodeId = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Zaehler";
      private const string WatchdogNodeId = "ns=1;s=LagerV.DataBlocks.OPC_Daten_ORG.Static.Watchdog";

      private readonly ILogger<OPC_Client_Crane> _logger;
      private readonly object _syncRoot = new();
      private readonly List<OpcMonitoredItem> monitoredItems = new();
      private OpcClient? client = null;
      private OpcSubscription? subscription = null;
      private bool spsDataUnavailable;
      private bool disposed;

      public OPC_Client_Crane(ILogger<OPC_Client_Crane> logger)
      {
         _logger = logger;
         TraegerLicense();

         // 1. Client instanziieren
         this.client = new OpcClient("opc.tcp://DEV11");

         // 2. Automatisches Reconnect-Verhalten konfigurieren
         // Das SDK aktiviert das Auto-Reconnect automatisch, sobald ein Timeout gesetzt ist.
         // Wir versuchen alle 5 Sekunden (5000 ms) eine Wiederverbindung.
         this.client.ReconnectTimeout = 5000;

         // 3. Statusänderungen überwachen (ersetzt Connected / Disconnected)
         this.client.StateChanged += OnClientStateChanged;

         _logger.LogInformation("OPC_Client_Crane initialisiert. Bereit für Connect().");
      }

      /// <summary>
      /// Startet den Verbindungsaufbau. Sollte von außen (z.B. im Worker-Start) aufgerufen werden.
      /// </summary>
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
               _logger.LogInformation("OPC-Verbindungsversuch {Attempt} wird gestartet.", attempt);
               ConnectOnce();
               ValidateWatchdogRead();
               _logger.LogInformation("OPC-Verbindungsversuch {Attempt} erfolgreich abgeschlossen.", attempt);
               return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
               throw;
            }
            catch (Exception ex)
            {
               _logger.LogError(ex, "OPC-Verbindungsversuch {Attempt} fehlgeschlagen. Naechster Versuch in {DelaySeconds} Sekunden.", attempt, ConnectRetryDelay.TotalSeconds);
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
               ValidateWatchdogRead();

               if (spsDataUnavailable)
               {
                  spsDataUnavailable = false;
                  _logger.LogInformation("SPS-Daten sind wieder lesbar. OPC-Subscription ist wieder aktiv.");
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
               _logger.LogError(ex, "SPS-Datenpruefung fehlgeschlagen. Wiederherstellungsversuch {Attempt} in {DelaySeconds} Sekunden.", attempt, ConnectRetryDelay.TotalSeconds);

               await Task.Delay(ConnectRetryDelay, cancellationToken);

               try
               {
                  _logger.LogInformation("OPC-Wiederherstellungsversuch {Attempt} wird gestartet.", attempt);
                  ConnectOnce();
                  _logger.LogInformation("OPC-Wiederherstellungsversuch {Attempt} abgeschlossen. SPS-Daten werden erneut geprueft.", attempt);
               }
               catch (Exception reconnectEx)
               {
                  _logger.LogError(reconnectEx, "OPC-Wiederherstellungsversuch {Attempt} fehlgeschlagen.", attempt);
               }

               attempt++;
            }
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

            _logger.LogInformation("Verbindung zu opc.tcp://DEV11 wird aufgebaut.");
            client?.Connect();

            // Subscriptions einrichten
            subscription = client?.SubscribeNodes();

            // Deine Kanäle registrieren
            if (!ConnectChannels())
            {
               throw new InvalidOperationException("OPC-Kanal 'Zaehler' konnte nicht registriert werden.");
            }

            if (!ConnectChannels_WD_TEST())
            {
               throw new InvalidOperationException("OPC-Kanal 'Watchdog' konnte nicht registriert werden.");
            }

            _logger.LogInformation("OPC-Verbindung und Kanalregistrierung sind bereit.");
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
               throw new InvalidOperationException("OPC-Client ist nicht initialisiert.");
            }

            OpcValue watchdogValue = client.ReadNode(WatchdogNodeId);

            if (!watchdogValue.Status.IsGood)
            {
               throw new InvalidOperationException($"Watchdog-Node ist nicht sauber lesbar. Status={watchdogValue.Status.Code}, Wert={watchdogValue.Value}");
            }

            _logger.LogDebug("Watchdog-Node erfolgreich gelesen. Status={Status}, Wert={Value}", watchdogValue.Status.Code, watchdogValue.Value);
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
               _logger.LogInformation("OPC_Client_Crane wird heruntergefahren.");

               ResetSubscription();

               if (client is not null)
               {
                  client.StateChanged -= OnClientStateChanged;
                  client.Disconnect();
               }

               _logger.LogInformation("OPC_Client_Crane wurde getrennt.");
            }
            catch (Exception ex)
            {
               _logger.LogError(ex, "Fehler beim Herunterfahren des OPC_Client_Crane.");
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

            client?.Dispose();
            client = null;
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
            subscription.RemoveMonitoredItem(monitoredItems);
            subscription.ApplyChanges();
            monitoredItems.Clear();
         }

         subscription = null;
      }

      public Boolean ConnectChannels()
      {
         if (subscription == null || client == null) return false;

         var zaehlerItem = new OpcMonitoredItem(ZaehlerNodeId, OpcAttribute.Value);
         zaehlerItem.DataChangeReceived += HandleDataChange;

         subscription.AddMonitoredItem(zaehlerItem);
         monitoredItems.Add(zaehlerItem);
         subscription.ApplyChanges();

         _logger.LogInformation("Kanal 'Zähler' erfolgreich registriert.");
         return true;
      }

      public Boolean ConnectChannels_WD_TEST()
      {
         if (subscription == null || client == null) return false;

         var item = new OpcMonitoredItem(WatchdogNodeId, OpcAttribute.Value)
         {
            Tag = "craneEvent"
         };
         item.DataChangeReceived += HandleDataChange;

         subscription.AddMonitoredItem(item);
         monitoredItems.Add(item);
         subscription.ApplyChanges();

         _logger.LogInformation("Kanal 'Watchdog' erfolgreich registriert.");
         return true;
      }

      private void HandleDataChange(object sender, OpcDataChangeReceivedEventArgs e)
      {
         if (client == null) return;

         try
         {
            var neuerZaehlerWert = e.Item.Value.Value;
            _logger.LogInformation("Event ausgelöst von [{Node}]. Wert: {Z}", e.MonitoredItem.NodeId, neuerZaehlerWert);

            // Nur nachlesen, wenn es sich um den Zähler handelt
            if (e.MonitoredItem.NodeId.ToString().Contains("ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Zaehler"))
            {
               string baseNode = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.";

               var cmdStart = client.ReadNode($"{baseNode}Comands.Start")?.Value;
               var cmdStop = client.ReadNode($"{baseNode}Comands.Stop")?.Value;
               var statusRun = client.ReadNode($"{baseNode}Status.Run")?.Value;

               _logger.LogInformation("Werte erfolgreich nachgelesen - Start: {Start}, Stop: {Stop}, Run: {Run}",
                   cmdStart, cmdStop, statusRun);

               if (statusRun is bool isRunning && isRunning)
               {
                  // Deine Kran-Logik
               }
            }
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, "Fehler im HandleDataChange beim Nachlesen der SPS-Daten.");
         }
      }

      

      #region Verbindungs-Event-Handler

      private void OnClientStateChanged(object? sender, OpcClientStateChangedEventArgs e)
      {
         _logger.LogDebug("OPC Client Zustand geändert von {OldState} zu {NewState}", e.OldState, e.NewState);

         // Hier fangen wir die Zustände für dein Logging sauber ab
         if (e.NewState == OpcClientState.Connected)
         {
            _logger.LogWarning("OPC UA Client erfolgreich verbunden / wiederverbunden!");
         }
         else if (e.NewState == OpcClientState.Disconnected)
         {
            _logger.LogError("Die Verbindung zum OPC UA Server wurde getrennt!");
         }
         else if (e.NewState == OpcClientState.Reconnecting)
         {
            _logger.LogInformation("Verbindung verloren. Auto-Reconnect versucht gerade die Wiederverbindung...");
         }
      }

      #endregion

      static void TraegerLicense()
      {
         Opc.UaFx.Client.Licenser.LicenseKey = ConfigManager.TraegerLicenseKey;
         var license = Opc.UaFx.Client.Licenser.LicenseInfo;
         // In produktiven Apps lieber loggen statt Console.WriteLine
         Console.WriteLine("Traeger Licence Info = {0} | Gültig = {1}", license.ToString(), !license.IsEvaluation);
      }
   }
}
