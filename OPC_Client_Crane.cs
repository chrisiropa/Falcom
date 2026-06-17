using Opc.UaFx;
using Opc.UaFx.Client;

namespace Falcom
{
   public sealed class OPC_Client_Crane : IDisposable
   {
      private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(5);
      private const string ZaehlerNodeId = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Zaehler";
      private const string WatchdogNodeId = "ns=1;s=LagerV.DataBlocks.OPC_Daten_ORG.Static.Watchdog";
      private const string KranfahrtBeendetNodeId = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Comands.Stop";

      private readonly ILogger<OPC_Client_Crane> _logger;
      private readonly FalcomEventQueue _eventQueue; // NEU: Privates Feld für die Queue
      private readonly object _syncRoot = new();
      private readonly List<OpcMonitoredItem> monitoredItems = new();
      private readonly string opcServerEndpoint;
      private OpcClient? client = null;
      private OpcSubscription? subscription = null;
      private bool spsDataUnavailable;
      private bool disposed;

      // NEU: FalcomEventQueue im Konstruktor anfordern
      public OPC_Client_Crane(ILogger<OPC_Client_Crane> logger, Parameter parameter, FalcomEventQueue eventQueue)
      {
         _logger = logger;
         _eventQueue = eventQueue; // NEU: Zuweisung für den späteren Zugriff
         TraegerLicense();

         if (string.IsNullOrWhiteSpace(parameter.OpcServer))
         {
            throw new InvalidOperationException("Der Datenbankparameter 'OpcServer' ist leer oder wurde nicht gefunden.");
         }

         opcServerEndpoint = parameter.OpcServer.Trim();

         // Client-Instanz das erste Mal erstellen
         CreateClientInstance();

         _logger.LogInformation("OPC_Client_Crane initialisiert fuer {OpcServerEndpoint}. Bereit fuer Connect().", opcServerEndpoint);
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
                  _logger.LogInformation("SPS-Daten sind wieder lesbar. OPC-Subscription is wieder aktiv.");
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
                  _logger.LogInformation("OPC-Wiederherstellungsversuch {Attempt} wird gestartet (Radikaler Reconnect).", attempt);

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

            _logger.LogInformation("Verbindung zu {OpcServerEndpoint} wird aufgebaut.", opcServerEndpoint);
            client?.Connect();

            subscription = client?.SubscribeNodes();

            if (!ConnectChannels())
            {
               throw new InvalidOperationException("OPC-Kanal 'Zaehler' konnte nicht registriert werden.");
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

            _logger.LogInformation("Watchdog-Node erfolgreich gelesen. Status={Status}, Wert={Value}", watchdogValue.Status.Code, watchdogValue.Value);
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
               _logger.LogDebug(ex, "Alte Subscription konnte wegen totem Kanal nicht sauber entfernt werden. Wird erzwungen.");
            }

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

         var kranfahrtBeendetItem = new OpcMonitoredItem(KranfahrtBeendetNodeId, OpcAttribute.Value);
         kranfahrtBeendetItem.DataChangeReceived += HandleDataChange;
         subscription.AddMonitoredItem(kranfahrtBeendetItem);
         monitoredItems.Add(kranfahrtBeendetItem);

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

            if (e.MonitoredItem.NodeId.ToString().Contains("ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.Zaehler"))
            {
               string baseNode = "ns=1;s=LagerV.DataBlocks.Count_DB_1.Static.OPC.";

               var cmdStart = client.ReadNode($"{baseNode}Comands.Start")?.Value;
               var cmdStop = client.ReadNode($"{baseNode}Comands.Stop")?.Value;
               var statusRun = client.ReadNode($"{baseNode}Status.Run")?.Value;

               _logger.LogInformation("Werte erfolgreich nachgelesen - Start: {Start}, Stop: {Stop}, Run: {Run}", cmdStart, cmdStop, statusRun);

               if (statusRun is bool isRunning && isRunning)
               {
                  // Deine Kran-Logik
               }
            }
            else if (e.MonitoredItem.NodeId.ToString().Contains(KranfahrtBeendetNodeId))
            {
               _logger.LogInformation("Kranfahrt beendet");
               var kranEvent = new KranfahrtBeendetEvent(
                    auftragsNummer: 1,
                    kranQuelle: "BOX 4",
                    toleranz: 50.0,
                    istGewicht: 555.0,
                    fehlercode: 0
               );

               // NEU: Jetzt über das injizierte Feld asynchron in den Channel schieben
               // Da HandleDataChange synchron ist, nutzen wir das im Channel vorgesehene Verhalten
               _eventQueue.Writer.WriteAsync(kranEvent, cancellationToken: default).AsTask().Wait();
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
         Console.WriteLine("Traeger Licence Info = {0} | Gültig = {1}", license.ToString(), !license.IsEvaluation);
      }
   }
}