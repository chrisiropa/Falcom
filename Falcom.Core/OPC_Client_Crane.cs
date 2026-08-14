using Microsoft.Data.SqlClient;
using Opc.UaFx;
using Opc.UaFx.Client;
using System.Data;
using System.Globalization;

namespace Falcom
{
   public sealed class OPC_Client_Crane : IDisposable
   {
      private static readonly TimeSpan ConnectRetryDelay = TimeSpan.FromSeconds(5);
      private static readonly TimeSpan Event201StartupGracePeriod = TimeSpan.FromSeconds(15);
      private static readonly TimeSpan Event201ReconnectTimeout = TimeSpan.FromSeconds(25);
      private const int Event101Id = 101;
      private const string Event101Direction = "FALCOM->KRAN_SPS";
      private const int Event301Id = 301;
      private const string Event301Direction = "FALCOM->CW";
      private const int Event203Id = 203;
      private const string Event203Name = "Event_203";
      private const string Event203Direction = "KRAN_SPS->FALCOM";
      private const string PosKranNodeName = "PosKran";
      private const string PosKatzeNodeName = "PosKatze";
      private const string PosHubNodeName = "PosHub";
      private const string MagnetAnNodeName = "MagnetAn";
      private const string MasseNettoNodeName = "MasseNetto";
      private const string Event203TriggerNodeName = "Event_203";
      private const string Event104Name = "Event_104";
      private const string Event104Direction = "FALCOM->KRAN_SPS";
      private const string Event104TriggerNodeName = "Event_104";
      private const string Event105Name = "Event_105";
      private const string Event105Direction = "FALCOM->KRAN_SPS";
      private const string Event105TriggerNodeName = "Event_105";
      private const int Event105MaxMaterialIndex = 20;
      private static readonly DateTime EmptyEvent105DatumZeit = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
      private const string Event204Name = "Event_204";
      private const string Event204Direction = "KRAN_SPS->FALCOM";
      private const string Event204TriggerNodeName = "Event_204";
      private const string Event205Name = "Event_205";
      private const string Event205Direction = "KRAN_SPS->FALCOM";
      private const string Event205TriggerNodeName = "Event_205";
      private const string Event106Name = "Event_106";
      private const string Event106Direction = "FALCOM->KRAN_SPS";
      private const string Event106TriggerNodeName = "Event_106";
      private const int Event106MaxObjectIndex = 50;
      private const int Event106MaxPositionIndex = 9;
      private const string Event206Name = "Event_206";
      private const string Event206Direction = "KRAN_SPS->FALCOM";
      private const string Event206TriggerNodeName = "Event_206";
      private const string Event401Name = "Event_401";
      private const string Event401Direction = "CW->FALCOM";
      private const string Event401TriggerNodeName = "Event_401";
      private const string Event402Name = "Event_402";
      private const string Event402Direction = "CW->FALCOM";
      private const string Event402TriggerNodeName = "Event_402";
      private const string Event403Name = "Event_403";
      private const string Event403Direction = "CW->FALCOM";
      private const string Event403TriggerNodeName = "Event_403";
      private const string Event404Name = "Event_404";
      private const string Event404Direction = "CW->FALCOM";
      private const string Event404TriggerNodeName = "Event_404";
      private const string Event405Name = "Event_405";
      private const string Event405Direction = "CW->FALCOM";
      private const string Event405TriggerNodeName = "Event_405";
      private const int KranfahrtBeendetEventId = 202;
      private const int KranfahrtAuftragEventId = 102;
      private const string KranfahrtAuftragEventName = KranfahrtAuftragEvent.EventName;
      private const string KranfahrtAuftragDirection = "FALCOM->KRAN_SPS";
      private const string Event102TriggerNodeName = KranfahrtAuftragEvent.EventTriggerNodeName;

      private readonly ILogger<OPC_Client_Crane> _logger;
      private readonly ConfigManager _configManager;
      private readonly FalcomRuntimeStatus _runtimeStatus;
      private readonly FalcomKranLiveSignalRClient _kranLiveSignalRClient;
      private readonly AktuelleFahrtRepository _aktuelleFahrtRepository;
      private readonly FalcomEventQueue _eventQueue; // Privates Feld fuer die Queue
      private readonly object _syncRoot = new();
      private readonly object _cwSyncRoot = new();
      private readonly object _eOfenSyncRoot = new();
      private readonly List<OpcMonitoredItem> monitoredItems = new();
      private readonly List<OpcMonitoredItem> cwMonitoredItems = new();
      private readonly List<OpcMonitoredItem> eOfenMonitoredItems = new();
      private readonly string opcServerEndpoint;
      private readonly string event101NodeId;
      private readonly string event301NodeId;
      private readonly string event201NodeId;
      private readonly Dictionary<string, string> event203OpcNodesByName;
      private readonly Dictionary<string, string> event104OpcNodesByName;
      private readonly Dictionary<string, string> event105OpcNodesByName;
      private readonly Dictionary<string, string> event204OpcNodesByName;
      private readonly Dictionary<string, string> event205OpcNodesByName;
      private readonly Dictionary<string, string> event106OpcNodesByName;
      private readonly Dictionary<string, string> event206OpcNodesByName;
      private readonly Dictionary<string, string> event401OpcNodesByName;
      private readonly Dictionary<string, string> event402OpcNodesByName;
      private readonly Dictionary<string, string> event403OpcNodesByName;
      private readonly Dictionary<string, string> event404OpcNodesByName;
      private readonly Dictionary<string, string> event405OpcNodesByName;
      private readonly Dictionary<string, string> kranfahrtAuftragLiveOpcNodesByName;
      private OpcClient? client = null;
      private OpcClient? cwClient = null;
      private OpcClient? eOfenClient = null;
      private OpcSubscription? subscription = null;
      private OpcSubscription? cwSubscription = null;
      private OpcSubscription? eOfenSubscription = null;
      private bool spsDataUnavailable;
      private volatile bool spsLebensZaehlerFreigegeben;
      private DateTime nextDataFlowErrorLogUtc = DateTime.MinValue;
      private DateTime subscriptionCreatedUtc = DateTime.MinValue;
      private DateTime lastEvent201ReceivedUtc = DateTime.MinValue;
      private DateTime event201WatchdogFaultStartedUtc = DateTime.MinValue;
      private DateTime nextEvent201WatchdogLogUtc = DateTime.MinValue;
      private DateTime nextKranSpsLebensZaehlerLogUtc = DateTime.UtcNow.AddMinutes(1);
      private DateTime nextEvent203LogUtc = DateTime.UtcNow.AddMinutes(1);
      private DateTime nextEvent203ConfigurationLogUtc = DateTime.MinValue;
      private DateTime nextBackgroundReconnectLogUtc = DateTime.MinValue;
      private DateTime nextCwBackgroundReconnectLogUtc = DateTime.MinValue;
      private DateTime nextEOfenBackgroundReconnectLogUtc = DateTime.MinValue;
      private int kranfahrtAuftragTelegrammNummer;
      private bool kranfahrtAuftragZaehlerInitialisiert;
      private int kranSpsLebensZaehlerEventsInCurrentMinute;
      private int? lastEvent201Value;
      private int event203EventsInCurrentMinute;
      private int backgroundReconnectLoopRunning;
      private int cwBackgroundReconnectLoopRunning;
      private int eOfenBackgroundReconnectLoopRunning;
      private int? lastKranfahrtAuftragTelegrammNummer;
      private int? lastKranfahrtBeendetAenderungsZaehler;
      private bool kranfahrtBeendetInitialwertGesehen;
      private int? lastLkwPlatzLeerAenderungsZaehler;
      private bool lkwPlatzLeerInitialwertGesehen;
      private int? lastEvent204AnforderungsZaehler;
      private bool event204InitialwertGesehen;
      private int? lastEvent205AnforderungsZaehler;
      private bool event205InitialwertGesehen;
      private int? lastEvent206AnforderungsZaehler;
      private bool event206InitialwertGesehen;
      private int? lastEvent401AenderungsZaehler;
      private bool event401InitialwertGesehen;
      private int? lastEvent402AenderungsZaehler;
      private bool event402InitialwertGesehen;
      private int? lastEvent403AenderungsZaehler;
      private bool event403InitialwertGesehen;
      private int? lastEvent404AenderungsZaehler;
      private bool event404InitialwertGesehen;
      private int? lastEvent405AenderungsZaehler;
      private bool event405InitialwertGesehen;
      private int? aktuellePosKranX;
      private int? aktuellePosKatzeY;
      private int? aktuellePosHubZ;
      private int? aktuellerMagnetAn;
      private int? aktuelleMasseNetto;
      private string? lastKranfahrtAuftragConfigurationIssue;
      private readonly CancellationTokenSource backgroundReconnectCancellation = new();
      private bool disposed;

      // NEU: FalcomEventQueue im Konstruktor anfordern
      public OPC_Client_Crane(
         ILogger<OPC_Client_Crane> logger,
         Parameter parameter,
         ConfigManager configManager,
         FalcomRuntimeStatus runtimeStatus,
         FalcomKranLiveSignalRClient kranLiveSignalRClient,
         AktuelleFahrtRepository aktuelleFahrtRepository,
         FalcomEventQueue eventQueue)
      {
         _logger = logger;
         _configManager = configManager;
         _runtimeStatus = runtimeStatus;
         _kranLiveSignalRClient = kranLiveSignalRClient;
         _aktuelleFahrtRepository = aktuelleFahrtRepository;
         _eventQueue = eventQueue; // Zuweisung fuer den spaeteren Zugriff
         TraegerLicense();
         KranfahrtBeendetEvent.LoadOpcNodes(configManager);
         LkwPlatzLeer207Event.LoadOpcNodes(configManager);
         event101NodeId = LoadRequiredEventOpcNode(
            eventName: WatchdogEvent.EventName,
            direction: "FALCOM->KRAN_SPS",
            nodeName: WatchdogEvent.EventName);
         event301NodeId = LoadRequiredEventOpcNode(
            eventName: CwWatchdogEvent.EventName,
            direction: Event301Direction,
            nodeName: CwWatchdogEvent.EventName);
         event201NodeId = LoadRequiredEventOpcNode(
            eventName: KranSpsLebensZaehlerEvent.EventName,
            direction: "KRAN_SPS->FALCOM",
            nodeName: KranSpsLebensZaehlerEvent.EventName);
         event203OpcNodesByName = LoadOptionalEventOpcNodes(
            Event203Name,
            Event203Direction);
         event104OpcNodesByName = LoadOptionalEventOpcNodes(
            Event104Name,
            Event104Direction);
         event105OpcNodesByName = LoadOptionalEventOpcNodes(
            Event105Name,
            Event105Direction);
         event204OpcNodesByName = LoadOptionalEventOpcNodes(
            Event204Name,
            Event204Direction);
         event205OpcNodesByName = LoadOptionalEventOpcNodes(
            Event205Name,
            Event205Direction);
         event106OpcNodesByName = LoadOptionalEventOpcNodes(
            Event106Name,
            Event106Direction);
         event206OpcNodesByName = LoadOptionalEventOpcNodes(
            Event206Name,
            Event206Direction);
         event401OpcNodesByName = LoadOptionalEventOpcNodes(
            Event401Name,
            Event401Direction);
         event402OpcNodesByName = LoadOptionalEventOpcNodes(
            Event402Name,
            Event402Direction);
         event403OpcNodesByName = LoadOptionalEventOpcNodes(
            Event403Name,
            Event403Direction);
         event404OpcNodesByName = LoadOptionalEventOpcNodes(
            Event404Name,
            Event404Direction);
         event405OpcNodesByName = LoadOptionalEventOpcNodes(
            Event405Name,
            Event405Direction);
         kranfahrtAuftragLiveOpcNodesByName = LoadOptionalEventOpcNodes(
            KranfahrtAuftragEventName,
            KranfahrtAuftragDirection);

         if (string.IsNullOrWhiteSpace(parameter.OpcServer))
         {
            throw new InvalidOperationException("Der Datenbankparameter 'OpcServer' ist leer oder wurde nicht gefunden.");
         }

         opcServerEndpoint = parameter.OpcServer.Trim();

         // Client-Instanz das erste Mal erstellen
         CreateClientInstance();
         CreateCwClientInstance();
         CreateEOfenClientInstance();

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

         this.client = new OpcClient(opcServerEndpoint)
         {
            OperationTimeout = 5_000,
            SessionTimeout = 5_000,
            ReconnectTimeout = 5_000
         };
         this.client.StateChanged += OnClientStateChanged;
      }

      private void CreateCwClientInstance()
      {
         if (cwClient is not null)
         {
            cwClient.StateChanged -= OnCwClientStateChanged;
         }

         cwClient = new OpcClient(opcServerEndpoint)
         {
            OperationTimeout = 5_000,
            SessionTimeout = 5_000,
            ReconnectTimeout = 5_000
         };
         cwClient.StateChanged += OnCwClientStateChanged;
      }

      private void CreateEOfenClientInstance()
      {
         if (eOfenClient is not null)
         {
            eOfenClient.StateChanged -= OnEOfenClientStateChanged;
         }

         eOfenClient = new OpcClient(opcServerEndpoint)
         {
            OperationTimeout = 5_000,
            SessionTimeout = 5_000,
            ReconnectTimeout = 5_000
         };
         eOfenClient.StateChanged += OnEOfenClientStateChanged;
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
               TryConnectCwOrStartReconnect("Initialer Verbindungsaufbau");
               TryConnectEOfenOrStartReconnect("Initialer Verbindungsaufbau");
               MarkOpcDataFlowChecking("Verbunden, Datenfluss wird geprueft", "Event_201 wird geprueft");
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
               if (!EnsureEvent201SubscriptionDataFlow())
               {
                  return;
               }

               bool warDatenflussGestoert = spsDataUnavailable;
               MarkOpcDataFlowAvailable("Verbunden");

               if (warDatenflussGestoert)
               {
                  _logger.LogInformation("0015|OPC-Verbindung ist wieder verfuegbar. Subscription ist wieder aktiv.");
               }

               return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
               throw;
            }
            catch (Exception ex)
            {
               MarkOpcDataFlowUnavailable("Reconnect laeuft", "OPC-Datenfluss nicht freigegeben");

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

      private void StartBackgroundReconnectLoop(string reason)
      {
         if (disposed || backgroundReconnectCancellation.IsCancellationRequested)
         {
            return;
         }

         if (Interlocked.CompareExchange(
                ref backgroundReconnectLoopRunning,
                1,
                0) != 0)
         {
            return;
         }

         _logger.LogWarning(
            "005D|OPC-Hintergrund-Reconnect wird gestartet. Grund={Reason}.",
            reason);

         _ = Task.Run(
            async () =>
            {
               var attempt = 1;
               CancellationToken cancellationToken = backgroundReconnectCancellation.Token;

               try
               {
                  while (!disposed && !cancellationToken.IsCancellationRequested)
                  {
                     try
                     {
                        lock (_syncRoot)
                        {
                           if (client is { State: OpcClientState.Connected })
                           {
                              RecreateSubscription();
                              MarkOpcDataFlowChecking("Verbunden, Datenfluss wird geprueft", "Event_201 wird geprueft");
                              _logger.LogInformation(
                                 "005E|OPC-Hintergrund-Reconnect: Subscription neu registriert. Versuch={Attempt}. Warte auf Event_201.",
                                 attempt);
                              return;
                           }

                           ResetSubscription();

                           if (client is not null)
                           {
                              try
                              {
                                 client.StateChanged -= OnClientStateChanged;
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
                           MarkOpcDataFlowChecking("Verbunden, Datenfluss wird geprueft", "Event_201 wird geprueft");
                           _logger.LogInformation(
                              "0100|OPC-Hintergrund-Reconnect erfolgreich. Versuch={Attempt}. Warte auf Event_201.",
                              attempt);
                           return;
                        }
                     }
                     catch (Exception ex)
                     {
                        MarkOpcDataFlowUnavailable("Reconnect laeuft", "OPC Reconnect laeuft");

                        if (DateTime.UtcNow >= nextBackgroundReconnectLogUtc)
                        {
                           _logger.LogWarning(
                              "005F|OPC-Hintergrund-Reconnect Versuch={Attempt} noch nicht erfolgreich. Naechster Versuch in {DelaySeconds} Sekunden. Fehler={ExceptionType}: {Message}",
                              attempt,
                              ConnectRetryDelay.TotalSeconds,
                              ex.GetType().Name,
                              ex.Message);
                           nextBackgroundReconnectLogUtc = DateTime.UtcNow.AddMinutes(1);
                        }
                     }

                     attempt++;
                     await Task.Delay(ConnectRetryDelay, cancellationToken);
                  }
               }
               catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
               {
               }
               finally
               {
                  Interlocked.Exchange(ref backgroundReconnectLoopRunning, 0);
               }
            },
            backgroundReconnectCancellation.Token);
      }

      private void StartCwBackgroundReconnectLoop(string reason)
      {
         if (disposed || backgroundReconnectCancellation.IsCancellationRequested)
         {
            return;
         }

         if (Interlocked.CompareExchange(
                ref cwBackgroundReconnectLoopRunning,
                1,
                0) != 0)
         {
            return;
         }

         _logger.LogWarning(
            "0324|CW-OPC-Hintergrund-Reconnect wird gestartet. Grund={Reason}.",
            reason);

         _ = Task.Run(
            async () =>
            {
               var attempt = 1;
               CancellationToken cancellationToken = backgroundReconnectCancellation.Token;

               try
               {
                  while (!disposed && !cancellationToken.IsCancellationRequested)
                  {
                     try
                     {
                        lock (_cwSyncRoot)
                        {
                           ResetCwSubscription();

                           if (cwClient is not null)
                           {
                              try
                              {
                                 cwClient.StateChanged -= OnCwClientStateChanged;
                                 cwClient.Disconnect();
                              }
                              catch
                              {
                              }

                              cwClient.Dispose();
                              cwClient = null;
                           }

                           CreateCwClientInstance();
                           ConnectCwOnce();
                           _runtimeStatus.SetOpcCwSpsStatus(true, "Verbunden");
                           _logger.LogInformation(
                              "0325|CW-OPC-Hintergrund-Reconnect erfolgreich. Versuch={Attempt}.",
                              attempt);
                           return;
                        }
                     }
                     catch (Exception ex)
                     {
                        _runtimeStatus.SetCwLebensZaehlerUnavailable("CW Reconnect laeuft");

                        if (DateTime.UtcNow >= nextCwBackgroundReconnectLogUtc)
                        {
                           _logger.LogWarning(
                              "0326|CW-OPC-Hintergrund-Reconnect Versuch={Attempt} noch nicht erfolgreich. Naechster Versuch in {DelaySeconds} Sekunden. Fehler={ExceptionType}: {Message}",
                              attempt,
                              ConnectRetryDelay.TotalSeconds,
                              ex.GetType().Name,
                              ex.Message);
                           nextCwBackgroundReconnectLogUtc = DateTime.UtcNow.AddMinutes(1);
                        }
                     }

                     attempt++;
                     await Task.Delay(ConnectRetryDelay, cancellationToken);
                  }
               }
               catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
               {
               }
               finally
               {
                  Interlocked.Exchange(ref cwBackgroundReconnectLoopRunning, 0);
               }
            },
            backgroundReconnectCancellation.Token);
      }

      private void StartEOfenBackgroundReconnectLoop(string reason)
      {
         if (disposed || backgroundReconnectCancellation.IsCancellationRequested)
         {
            return;
         }

         if (Interlocked.CompareExchange(
                ref eOfenBackgroundReconnectLoopRunning,
                1,
                0) != 0)
         {
            return;
         }

         _logger.LogWarning(
            "0524|E-Ofen-OPC-Hintergrund-Reconnect wird gestartet. Grund={Reason}.",
            reason);

         _ = Task.Run(
            async () =>
            {
               var attempt = 1;
               CancellationToken cancellationToken = backgroundReconnectCancellation.Token;

               try
               {
                  while (!disposed && !cancellationToken.IsCancellationRequested)
                  {
                     try
                     {
                        lock (_eOfenSyncRoot)
                        {
                           ResetEOfenSubscription();

                           if (eOfenClient is not null)
                           {
                              try
                              {
                                 eOfenClient.StateChanged -= OnEOfenClientStateChanged;
                                 eOfenClient.Disconnect();
                              }
                              catch
                              {
                              }

                              eOfenClient.Dispose();
                              eOfenClient = null;
                           }

                           CreateEOfenClientInstance();
                           ConnectEOfenOnce();
                           _runtimeStatus.SetOpcEOfenSpsStatus(true, "Verbunden");
                           _logger.LogInformation(
                              "0525|E-Ofen-OPC-Hintergrund-Reconnect erfolgreich. Versuch={Attempt}.",
                              attempt);
                           return;
                        }
                     }
                     catch (Exception ex)
                     {
                        _runtimeStatus.SetEOfenLebensZaehlerUnavailable("E-Ofen Reconnect laeuft");

                        if (DateTime.UtcNow >= nextEOfenBackgroundReconnectLogUtc)
                        {
                           _logger.LogWarning(
                              "0526|E-Ofen-OPC-Hintergrund-Reconnect Versuch={Attempt} noch nicht erfolgreich. Naechster Versuch in {DelaySeconds} Sekunden. Fehler={ExceptionType}: {Message}",
                              attempt,
                              ConnectRetryDelay.TotalSeconds,
                              ex.GetType().Name,
                              ex.Message);
                           nextEOfenBackgroundReconnectLogUtc = DateTime.UtcNow.AddMinutes(1);
                        }
                     }

                     attempt++;
                     await Task.Delay(ConnectRetryDelay, cancellationToken);
                  }
               }
               catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
               {
               }
               finally
               {
                  Interlocked.Exchange(ref eOfenBackgroundReconnectLoopRunning, 0);
               }
            },
            backgroundReconnectCancellation.Token);
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
            EnsureKranfahrtAuftragZaehlerInitialisiert(nodes);

            int telegrammNummer = kranfahrtAuftragTelegrammNummer == int.MaxValue
               ? 0
               : kranfahrtAuftragTelegrammNummer + 1;

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
            WriteRequiredNode(nodes.QuelleUnterposition, kranfahrtAuftragEvent.QuelleUnterposition);
            WriteRequiredNode(nodes.ZielUnterposition, kranfahrtAuftragEvent.ZielUnterposition);
            WriteRequiredNode(
               nodes.SollMasse,
               decimal.ToInt32(decimal.Round(kranfahrtAuftragEvent.SollMasseKg, 0, MidpointRounding.AwayFromZero)));
            WriteRequiredNode(nodes.MasseTolPos, kranfahrtAuftragEvent.MasseTolPosKg);
            WriteRequiredNode(nodes.MasseTolNeg, kranfahrtAuftragEvent.MasseTolNegKg);
            WriteRequiredNode(nodes.MaterialNr, kranfahrtAuftragEvent.MaterialNr);

            // Event_102 ist der eigentliche Trigger fuer die Kran-SPS/Simulation.
            // Deshalb bewusst zuletzt schreiben, nachdem alle Payload-Werte stehen.
            WriteRequiredNode(nodes.EventTrigger, telegrammNummer);

            kranfahrtAuftragTelegrammNummer = telegrammNummer;

            _logger.LogInformation(               "0047|Event_102 an SPS gesendet: Nr={AuftragID}, TeilNr={AuftragTeilfahrt}, Quelle={QuellePositionID}, QuelleUnterposition={QuelleUnterposition}, Ziel={ZielPositionID}, ZielUnterposition={ZielUnterposition}, SollMasse={SollMasseKg}, MasseTol_pos={MasseTolPosKg}, MasseTol_neg={MasseTolNegKg}, MaterialNr={MaterialNr}, Event_102={TelegrammNummer}.",
               kranfahrtAuftragEvent.AuftragNummer,
               kranfahrtAuftragEvent.AuftragTeilfahrt,
               kranfahrtAuftragEvent.QuellePositionID,
               kranfahrtAuftragEvent.QuelleUnterposition,
               kranfahrtAuftragEvent.ZielPositionID,
               kranfahrtAuftragEvent.ZielUnterposition,
               kranfahrtAuftragEvent.SollMasseKg,
               kranfahrtAuftragEvent.MasseTolPosKg,
               kranfahrtAuftragEvent.MasseTolNegKg,
               kranfahrtAuftragEvent.MaterialNr,
               telegrammNummer);

            return Task.FromResult(OpcSendResult.Ok(telegrammNummer));
         }
         catch (Exception ex)
         {
            _logger.LogError(
               ex,
               "0048|KranfahrtAuftrag konnte nicht an die SPS gesendet werden. Auftrag={AuftragID}, QuellePositionID={QuellePositionID}, QuelleUnterposition={QuelleUnterposition}, ZielPositionID={ZielPositionID}, ZielUnterposition={ZielUnterposition}.",
               kranfahrtAuftragEvent.AuftragNummer,
               kranfahrtAuftragEvent.QuellePositionID,
               kranfahrtAuftragEvent.QuelleUnterposition,
               kranfahrtAuftragEvent.ZielPositionID,
               kranfahrtAuftragEvent.ZielUnterposition);
            return Task.FromResult(OpcSendResult.Failed(
               $"KranfahrtAuftrag konnte nicht an die SPS gesendet werden: {ex.Message}"));
         }
      }

      public Task<OpcSendResult> SendFalcomLebensZaehlerAsync(
         int lebensZaehler,
         CancellationToken cancellationToken)
      {
         cancellationToken.ThrowIfCancellationRequested();

         try
         {
            EnsureConnected();

            lock (_syncRoot)
            {
               WriteRequiredNode(event101NodeId, lebensZaehler);
            }

            _ = _kranLiveSignalRClient.SendKranOpcEventAsync(
               Event101Id,
               WatchdogEvent.EventName,
               Event101Direction,
               WatchdogEvent.EventName,
               lebensZaehler,
               new Dictionary<string, object?>
               {
                  [WatchdogEvent.EventName] = lebensZaehler
               },
               CancellationToken.None);

            return Task.FromResult(OpcSendResult.Ok());
         }
         catch (Exception ex)
         {
            MarkOpcDataFlowUnavailable("Reconnect laeuft", "Event_101 konnte nicht geschrieben werden");
            StartBackgroundReconnectLoop("Event_101 konnte nicht geschrieben werden");

            return Task.FromResult(
               OpcSendResult.Failed(
                  $"Event_101 konnte nicht an die Kran-SPS gesendet werden. Node={event101NodeId}, Wert={lebensZaehler}, Fehler={ex.GetType().Name}: {ex.Message}"));
         }
      }

      public Task<OpcSendResult> SendFalcomCwLebensZaehlerAsync(
         int lebensZaehler,
         CancellationToken cancellationToken)
      {
         cancellationToken.ThrowIfCancellationRequested();

         try
         {
            EnsureCwConnected();

            lock (_cwSyncRoot)
            {
               WriteRequiredNode(cwClient!, event301NodeId, lebensZaehler);
            }

            _ = _kranLiveSignalRClient.SendKranOpcEventAsync(
               Event301Id,
               CwWatchdogEvent.EventName,
               Event301Direction,
               CwWatchdogEvent.EventName,
               lebensZaehler,
               new Dictionary<string, object?>
               {
                  [CwWatchdogEvent.EventName] = lebensZaehler
               },
               CancellationToken.None);

            _runtimeStatus.SetOpcCwSpsStatus(true, "Verbunden");
            return Task.FromResult(OpcSendResult.Ok());
         }
         catch (Exception ex)
         {
            _runtimeStatus.SetCwLebensZaehlerUnavailable("Event_301 konnte nicht geschrieben werden");
            StartCwBackgroundReconnectLoop("Event_301 konnte nicht geschrieben werden");

            return Task.FromResult(
               OpcSendResult.Failed(
                  $"Event_301 konnte nicht an die CW-SPS gesendet werden. Node={event301NodeId}, Wert={lebensZaehler}, Fehler={ex.GetType().Name}: {ex.Message}"));
         }
      }

      public Task<OpcSendResult> SendBunkerMaterialResponseAsync(
         int anforderungsZaehler,
         BunkerMaterialSnapshot snapshot,
         CancellationToken cancellationToken)
      {
         cancellationToken.ThrowIfCancellationRequested();

         try
         {
            EnsureConnected();

            lock (_syncRoot)
            {
               for (int index = 0; index < 21; index++)
               {
                  BunkerMaterialEintrag? eintrag = snapshot.Eintraege
                     .FirstOrDefault(item => item.ArrayIndex == index);

                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event104OpcNodesByName, Event104Name, $"Bunker{index:00}_BuNr"),
                     eintrag?.BuNr ?? 0);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event104OpcNodesByName, Event104Name, $"Bunker{index:00}_MaterialNr"),
                     eintrag?.MaterialNr ?? 0);
               }

               // Der korrelierte Trigger wird bewusst zuletzt geschrieben.
               WriteRequiredNode(
                  GetRequiredConfiguredEventNode(event104OpcNodesByName, Event104Name, Event104TriggerNodeName),
                  anforderungsZaehler);
            }
            _logger.LogInformation(
               "01D7|Event_104 an Kran-SPS gesendet: AnforderungsZaehler={AnforderungsZaehler}, AnzahlBunker={AnzahlBunker}, belegteArrayPlaetze={BelegteArrayPlaetze}.",
               anforderungsZaehler,
               snapshot.AnzahlBunker,
               string.Join(",", snapshot.Eintraege.Select(item => item.ArrayIndex)));

            return Task.FromResult(OpcSendResult.Ok(anforderungsZaehler));
         }
         catch (Exception ex)
         {
            _logger.LogError(
               "01D8|Event_104 konnte nicht an die Kran-SPS gesendet werden. AnforderungsZaehler={AnforderungsZaehler}, Fehler={ExceptionType}: {Message}.",
               anforderungsZaehler,
               ex.GetType().Name,
               ex.Message);
            return Task.FromResult(OpcSendResult.Failed(ex.Message));
         }
      }

      public Task<OpcSendResult> SendMaterialEigenschaftenResponseAsync(
         int anforderungsZaehler,
         MaterialEigenschaftenSnapshot snapshot,
         CancellationToken cancellationToken)
      {
         cancellationToken.ThrowIfCancellationRequested();

         try
         {
            EnsureConnected();

            IReadOnlyDictionary<int, MaterialEigenschaftenEintrag> eintraegeByArrayIndex = snapshot.Eintraege
               .Where(item => item.ArrayIndex >= 0 && item.ArrayIndex <= Event105MaxMaterialIndex)
               .ToDictionary(item => item.ArrayIndex);

            lock (_syncRoot)
            {
               for (int index = 0; index <= Event105MaxMaterialIndex; index++)
               {
                  eintraegeByArrayIndex.TryGetValue(index, out MaterialEigenschaftenEintrag? eintrag);

                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenNodeName(index, "iID")),
                     eintrag?.ID ?? 0);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenNodeName(index, "Mat_Name")),
                     eintrag?.MatName ?? string.Empty);
                  WriteRequiredDateTimeNodeWithStringFallback(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenNodeName(index, "Datum_Zeit")),
                     eintrag?.DatumZeit,
                     GetMaterialEigenschaftenNodeName(index, "Datum_Zeit"));
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "diMasseGattMin")),
                     eintrag?.DiMasseGattMin ?? 0);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "diZeitAbtippen")),
                     eintrag?.DiZeitAbtippen ?? 0);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "diMasseVorAbtippen")),
                     eintrag?.DiMasseVorAbtippen ?? 0);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "diMasseTol_pos")),
                     eintrag?.DiMasseTolPos ?? 0);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "diMasseTol_neg")),
                     eintrag?.DiMasseTolNeg ?? 0);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "rKraftStufenPro100Kg")),
                     eintrag?.RKraftStufenPro100Kg ?? 0f);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "xAbwurfFlach")),
                     eintrag?.XAbwurfFlach ?? false);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "xAbwurfAbzett")),
                     eintrag?.XAbwurfAbzett ?? false);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "xAbwurfTippen")),
                     eintrag?.XAbwurfTippen ?? false);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "xNachfassenMag_Aus")),
                     eintrag?.XNachfassenMagAus ?? false);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "xNachfassenMag_Dauernd")),
                     eintrag?.XNachfassenMagDauernd ?? false);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "xKreislauf")),
                     eintrag?.XKreislauf ?? false);
               }

               // Der korrelierte Trigger wird bewusst zuletzt geschrieben.
               WriteRequiredNode(
                  GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, Event105TriggerNodeName),
                  anforderungsZaehler);
            }
            _logger.LogInformation(
               "0209|Event_105 an Kran-SPS gesendet: AnforderungsZaehler={AnforderungsZaehler}, AnzahlMaterialien={AnzahlMaterialien}.",
               anforderungsZaehler,
               snapshot.AnzahlMaterialien);

            return Task.FromResult(OpcSendResult.Ok(anforderungsZaehler));
         }
         catch (Exception ex)
         {
            _logger.LogError(
               "020A|Event_105 konnte nicht an die Kran-SPS gesendet werden. AnforderungsZaehler={AnforderungsZaehler}, Fehler={ExceptionType}: {Message}.",
               anforderungsZaehler,
               ex.GetType().Name,
               ex.Message);
            return Task.FromResult(OpcSendResult.Failed(ex.Message));
         }
      }

      public Task<OpcReadResult<MaterialEigenschaftenSnapshot>> ReadMaterialEigenschaftenFromSpsAsync(
         CancellationToken cancellationToken)
      {
         cancellationToken.ThrowIfCancellationRequested();

         try
         {
            EnsureConnected();

            var eintraege = new List<MaterialEigenschaftenEintrag>();

            lock (_syncRoot)
            {
               for (int index = 1; index <= Event105MaxMaterialIndex; index++)
               {
                  int id = ReadRequiredInt32Node(
                     GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenNodeName(index, "iID")));
                  if (id <= 0)
                  {
                     continue;
                  }

                  eintraege.Add(new MaterialEigenschaftenEintrag(
                     index,
                     id,
                     ReadRequiredStringNode(GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenNodeName(index, "Mat_Name"))),
                     ReadRequiredNullableDateTimeNode(GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenNodeName(index, "Datum_Zeit"))),
                     ReadRequiredInt32Node(GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "diMasseGattMin"))),
                     ReadRequiredInt32Node(GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "diZeitAbtippen"))),
                     ReadRequiredInt32Node(GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "diMasseVorAbtippen"))),
                     ReadRequiredInt32Node(GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "diMasseTol_pos"))),
                     ReadRequiredInt32Node(GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "diMasseTol_neg"))),
                     ReadRequiredSingleNode(GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "rKraftStufenPro100Kg"))),
                     ReadRequiredBooleanNode(GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "xAbwurfFlach"))),
                     ReadRequiredBooleanNode(GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "xAbwurfAbzett"))),
                     ReadRequiredBooleanNode(GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "xAbwurfTippen"))),
                     ReadRequiredBooleanNode(GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "xNachfassenMag_Aus"))),
                     ReadRequiredBooleanNode(GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "xNachfassenMag_Dauernd"))),
                     ReadRequiredBooleanNode(GetRequiredConfiguredEventNode(event105OpcNodesByName, Event105Name, GetMaterialEigenschaftenParaNodeName(index, "xKreislauf")))));
               }
            }

            _logger.LogInformation(
               "0210|Event_105 Materialeigenschaften aus Kran-SPS gelesen. AnzahlMaterialien={AnzahlMaterialien}.",
               eintraege.Count);

            return Task.FromResult(OpcReadResult<MaterialEigenschaftenSnapshot>.Ok(new MaterialEigenschaftenSnapshot(eintraege)));
         }
         catch (Exception ex)
         {
            _logger.LogError(
               "0211|Event_105 Materialeigenschaften konnten nicht aus der Kran-SPS gelesen werden. Fehler={ExceptionType}: {Message}.",
               ex.GetType().Name,
               ex.Message);
            return Task.FromResult(OpcReadResult<MaterialEigenschaftenSnapshot>.Failed(ex.Message));
         }
      }

      public Task<OpcSendResult> SendKranPositionenResponseAsync(
         int anforderungsZaehler,
         KranPositionenSnapshot snapshot,
         CancellationToken cancellationToken)
      {
         cancellationToken.ThrowIfCancellationRequested();

         try
         {
            EnsureConnected();

            int maxIndex = Math.Max(Event106MaxObjectIndex, GetMaxConfiguredKranPositionenIndex());
            IReadOnlyDictionary<int, KranPositionenEintrag> eintraegeByArrayIndex = snapshot.Eintraege
               .Where(item => item.ArrayIndex >= 0 && item.ArrayIndex <= maxIndex)
               .ToDictionary(item => item.ArrayIndex);

            lock (_syncRoot)
            {
               for (int index = 0; index <= maxIndex; index++)
               {
                  eintraegeByArrayIndex.TryGetValue(index, out KranPositionenEintrag? eintrag);

                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event106OpcNodesByName, Event106Name, GetKranPositionenNodeName(index, "stArt")),
                     eintrag?.Art ?? string.Empty);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event106OpcNodesByName, Event106Name, GetKranPositionenNodeName(index, "stBezeichnung")),
                     eintrag?.Bezeichnung ?? string.Empty);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event106OpcNodesByName, Event106Name, GetKranPositionenNodeName(index, "stPositionsTyp")),
                     eintrag?.PositionsTyp ?? string.Empty);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event106OpcNodesByName, Event106Name, GetKranPositionenNodeName(index, "iID")),
                     eintrag?.ID ?? 0);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event106OpcNodesByName, Event106Name, GetKranPositionenNodeName(index, "diKatze_Start_X")),
                     eintrag?.DiKatzeStartX ?? 0);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event106OpcNodesByName, Event106Name, GetKranPositionenNodeName(index, "diKatze_Breite_X")),
                     eintrag?.DiKatzeBreiteX ?? 0);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event106OpcNodesByName, Event106Name, GetKranPositionenNodeName(index, "diKran_Start_Y")),
                     eintrag?.DiKranStartY ?? 0);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event106OpcNodesByName, Event106Name, GetKranPositionenNodeName(index, "diKran_Laenge_Y")),
                     eintrag?.DiKranLaengeY ?? 0);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event106OpcNodesByName, Event106Name, GetKranPositionenNodeName(index, "diHub_Start_Z")),
                     eintrag?.DiHubStartZ ?? 0);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event106OpcNodesByName, Event106Name, GetKranPositionenNodeName(index, "diHub_Hoehe_Z")),
                     eintrag?.DiHubHoeheZ ?? 0);
                  WriteRequiredNode(
                     GetRequiredConfiguredEventNode(event106OpcNodesByName, Event106Name, GetKranPositionenNodeName(index, "iPositionsAnz")),
                     eintrag?.PositionsAnz ?? 0);

                  IReadOnlyDictionary<int, KranPositionenUnterposition> positionenByArrayIndex = eintrag?.Positionen
                     .Where(position => position.ArrayIndex >= 0 && position.ArrayIndex <= Event106MaxPositionIndex)
                     .ToDictionary(position => position.ArrayIndex)
                     ?? new Dictionary<int, KranPositionenUnterposition>();

                  for (int positionIndex = 0; positionIndex <= Event106MaxPositionIndex; positionIndex++)
                  {
                     positionenByArrayIndex.TryGetValue(positionIndex, out KranPositionenUnterposition? position);

                     WriteRequiredNode(
                        GetRequiredConfiguredEventNode(event106OpcNodesByName, Event106Name, GetKranPositionenPositionNodeName(index, positionIndex, "diKatze_X")),
                        position?.DiKatzeX ?? 0);
                     WriteRequiredNode(
                        GetRequiredConfiguredEventNode(event106OpcNodesByName, Event106Name, GetKranPositionenPositionNodeName(index, positionIndex, "diKran_Y")),
                        position?.DiKranY ?? 0);
                  }
               }

               // Der korrelierte Trigger wird bewusst zuletzt geschrieben.
               WriteRequiredNode(
                  GetRequiredConfiguredEventNode(event106OpcNodesByName, Event106Name, Event106TriggerNodeName),
                  anforderungsZaehler);
            }
            _logger.LogInformation(
               "01F7|Event_106 an Kran-SPS gesendet: AnforderungsZaehler={AnforderungsZaehler}, AnzahlPositionen={AnzahlPositionen}, MaxIndex={MaxIndex}.",
               anforderungsZaehler,
               snapshot.AnzahlPositionen,
               maxIndex);

            return Task.FromResult(OpcSendResult.Ok(anforderungsZaehler));
         }
         catch (Exception ex)
         {
            _logger.LogError(
               "01F8|Event_106 konnte nicht an die Kran-SPS gesendet werden. AnforderungsZaehler={AnforderungsZaehler}, Fehler={ExceptionType}: {Message}.",
               anforderungsZaehler,
               ex.GetType().Name,
               ex.Message);
            return Task.FromResult(OpcSendResult.Failed(ex.Message));
         }
      }

      private static string GetRequiredConfiguredEventNode(
         IReadOnlyDictionary<string, string> nodes,
         string eventName,
         string nodeName)
      {
         if (!nodes.TryGetValue(nodeName, out string? nodeId)
             || !IsConfiguredOpcNode(nodeId))
         {
            throw new InvalidOperationException(
               $"OPC-Node fuer {eventName}.{nodeName} ist nicht gueltig konfiguriert.");
         }

         return nodeId.Trim();
      }

      private static string GetMaterialEigenschaftenNodeName(int index, string itemName)
      {
         return $"Material{index:000}_{itemName}";
      }

      private static string GetMaterialEigenschaftenParaNodeName(int index, string itemName)
      {
         return $"Material{index:000}_PARA_{itemName}";
      }
      private static string GetKranPositionenNodeName(int index, string itemName)
      {
         return $"Objekt{index:000}_{itemName}";
      }

      private static string GetKranPositionenPositionNodeName(int objektIndex, int positionIndex, string itemName)
      {
         return $"Objekt{objektIndex:000}_Positions{positionIndex:000}_{itemName}";
      }

      private int GetMaxConfiguredKranPositionenIndex()
      {
         int maxIndex = 0;
         foreach (string nodeName in event106OpcNodesByName.Keys)
         {
            if (!nodeName.StartsWith("Objekt", StringComparison.OrdinalIgnoreCase))
            {
               continue;
            }

            int separatorIndex = nodeName.IndexOf('_');
            if (separatorIndex <= "Objekt".Length)
            {
               continue;
            }

            string indexText = nodeName["Objekt".Length..separatorIndex];
            if (int.TryParse(indexText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index))
            {
               maxIndex = Math.Max(maxIndex, index);
            }
         }

         return maxIndex;
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

      private void EnsureCwConnected()
      {
         lock (_cwSyncRoot)
         {
            if (cwClient is { State: OpcClientState.Connected })
            {
               return;
            }

            _logger.LogInformation(
               "0322|OPC-CW-Client ist nicht verbunden. Verbindung wird vor der CW-Operation aufgebaut.");
            ConnectCwOnce();
         }
      }

      private void EnsureEOfenConnected()
      {
         lock (_eOfenSyncRoot)
         {
            if (eOfenClient is { State: OpcClientState.Connected })
            {
               return;
            }

            _logger.LogInformation(
               "0522|OPC-E-Ofen-Client ist nicht verbunden. Verbindung wird vor der E-Ofen-Operation aufgebaut.");
            ConnectEOfenOnce();
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
            KranfahrtAuftragEvent.QuelleUnterpositionNodeName,
            KranfahrtAuftragEvent.ZielUnterpositionNodeName,
            KranfahrtAuftragEvent.SollMasseNodeName,
            KranfahrtAuftragEvent.MasseTolPosNodeName,
            KranfahrtAuftragEvent.MasseTolNegNodeName,
            KranfahrtAuftragEvent.EventTriggerNodeName,
            KranfahrtAuftragEvent.MaterialNrNodeName
         ];

         foreach (string nodeName in requiredNodeNames)
         {
            if (!opcNodes.TryGetValue(nodeName, out string? opcNode)
                || string.IsNullOrWhiteSpace(opcNode)
                || opcNode.StartsWith("NOCH_ZU_KONFIGURIEREN.", StringComparison.OrdinalIgnoreCase)
                || string.Equals(opcNode, "Trigger Richtung SPS, einfach hochzaehlen", StringComparison.OrdinalIgnoreCase))
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
            opcNodes[KranfahrtAuftragEvent.QuelleUnterpositionNodeName].Trim(),
            opcNodes[KranfahrtAuftragEvent.ZielUnterpositionNodeName].Trim(),
            opcNodes[KranfahrtAuftragEvent.SollMasseNodeName].Trim(),
            opcNodes[KranfahrtAuftragEvent.MasseTolPosNodeName].Trim(),
            opcNodes[KranfahrtAuftragEvent.MasseTolNegNodeName].Trim(),
            opcNodes[KranfahrtAuftragEvent.EventTriggerNodeName].Trim(),
            opcNodes[KranfahrtAuftragEvent.MaterialNrNodeName].Trim());
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

      private void WriteRequiredDateTimeNodeWithStringFallback(
         string nodeId,
         DateTime? value,
         string nodeName)
      {
         DateTime normalizedValue = NormalizeEvent105DatumZeit(value);

         try
         {
            WriteRequiredNode(nodeId, normalizedValue);
         }
         catch (Exception ex)
         {
            string fallbackValue = normalizedValue.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            _logger.LogWarning(
               "020B|OPC Senden: Node {NodeName} akzeptiert DateTime aktuell nicht. Sende denselben Wert tolerant als ISO-Text. Node={Node}, Wert={Value}, Grund={Reason}",
               nodeName,
               nodeId,
               fallbackValue,
               ex.Message);

            WriteRequiredNode(nodeId, fallbackValue);
         }
      }

      private static DateTime NormalizeEvent105DatumZeit(DateTime? value)
      {
         if (value is null || value.Value <= EmptyEvent105DatumZeit)
         {
            return EmptyEvent105DatumZeit;
         }

         DateTime dateTime = value.Value;
         if (dateTime.Kind == DateTimeKind.Unspecified)
         {
            return DateTime.SpecifyKind(dateTime, DateTimeKind.Local);
         }

         return dateTime;
      }

      private void WriteRequiredNode(string nodeId, object value)
      {
         WriteRequiredNode(client!, nodeId, value);
      }

      private void WriteRequiredNode(OpcClient targetClient, string nodeId, object value)
      {
         _logger.LogInformation(
            "0051|OPC Senden: Node={Node}, Wert={Value}",
            nodeId,
            value);
         OpcStatus status = targetClient.WriteNode(nodeId, value);

         if (status.IsBad)
         {
            throw new InvalidOperationException(
               $"OPC-Schreiben fehlgeschlagen. Node={nodeId}, Status={status.Code}, Beschreibung={status.Description}");
         }

         _logger.LogInformation(
            "0052|OPC Schreiben vom OPC-Server angenommen: Node={Node}, Wert={Value}, Status={Status}",
            nodeId,
            value,
            status.Code);
      }

      private object? ReadRequiredNodeValue(string nodeId)
      {
         OpcValue value = client!.ReadNode(nodeId);
         if (!value.Status.IsGood)
         {
            throw new InvalidOperationException(
               $"OPC-Lesen fehlgeschlagen. Node={nodeId}, Status={value.Status.Code}, Beschreibung={value.Status.Description}");
         }

         return value.Value;
      }

      private int ReadRequiredInt32Node(string nodeId)
      {
         object? value = ReadRequiredNodeValue(nodeId);
         return value is null ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
      }

      private static int? ConvertToNullableInt32(object? value)
      {
         if (value is null)
         {
            return null;
         }

         try
         {
            return Convert.ToInt32(value, CultureInfo.InvariantCulture);
         }
         catch (Exception)
         {
            return null;
         }
      }

      private static bool? ConvertToNullableBoolean(object? value)
      {
         if (value is null)
         {
            return null;
         }

         try
         {
            return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
         }
         catch (Exception)
         {
            return null;
         }
      }

      private float ReadRequiredSingleNode(string nodeId)
      {
         object? value = ReadRequiredNodeValue(nodeId);
         return value is null ? 0f : Convert.ToSingle(value, CultureInfo.InvariantCulture);
      }

      private bool ReadRequiredBooleanNode(string nodeId)
      {
         object? value = ReadRequiredNodeValue(nodeId);
         return value is not null && Convert.ToBoolean(value, CultureInfo.InvariantCulture);
      }

      private string ReadRequiredStringNode(string nodeId)
      {
         object? value = ReadRequiredNodeValue(nodeId);
         return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
      }

      private DateTime? ReadRequiredNullableDateTimeNode(string nodeId)
      {
         object? value = ReadRequiredNodeValue(nodeId);
         if (value is null)
         {
            return null;
         }

         if (value is DateTime dateTime)
         {
            return dateTime <= EmptyEvent105DatumZeit ? null : dateTime;
         }

         string? text = Convert.ToString(value, CultureInfo.InvariantCulture);
         if (string.IsNullOrWhiteSpace(text))
         {
            return null;
         }

         return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out DateTime parsed)
            ? parsed
            : null;
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

            RecreateSubscription();
            RunStartupOpcNodeReadCheck();
            kranfahrtAuftragZaehlerInitialisiert = false;

            _logger.LogInformation("001B|OPC-Verbindung und Kanalregistrierung sind bereit.");
         }
      }

      private void TryConnectCwOrStartReconnect(string reason)
      {
         try
         {
            ConnectCwOnce();
         }
         catch (Exception ex)
         {
            _runtimeStatus.SetCwLebensZaehlerUnavailable("CW Reconnect laeuft");
            _logger.LogWarning(
               ex,
               "032B|CW-OPC-Verbindung konnte nicht aufgebaut werden. Hintergrund-Reconnect wird gestartet. Grund={Reason}.",
               reason);
            StartCwBackgroundReconnectLoop(reason);
         }
      }

      private void TryConnectEOfenOrStartReconnect(string reason)
      {
         try
         {
            ConnectEOfenOnce();
         }
         catch (Exception ex)
         {
            _runtimeStatus.SetEOfenLebensZaehlerUnavailable("E-Ofen Reconnect laeuft");
            _logger.LogWarning(
               ex,
               "052B|E-Ofen-OPC-Verbindung konnte nicht aufgebaut werden. Hintergrund-Reconnect wird gestartet. Grund={Reason}.",
               reason);
            StartEOfenBackgroundReconnectLoop(reason);
         }
      }

      private void ConnectCwOnce()
      {
         if (disposed)
         {
            throw new ObjectDisposedException(nameof(OPC_Client_Crane));
         }

         lock (_cwSyncRoot)
         {
            ResetCwSubscription();

            _logger.LogInformation("0320|Verbindung zur CW-SPS ueber {OpcServerEndpoint} wird aufgebaut.", opcServerEndpoint);
            cwClient?.Connect();

            RecreateCwSubscription();
            _runtimeStatus.SetOpcCwSpsStatus(true, "Verbunden");

            _logger.LogInformation("0321|CW-OPC-Verbindung und Kanalregistrierung sind bereit.");
         }
      }

      private void ConnectEOfenOnce()
      {
         if (disposed)
         {
            throw new ObjectDisposedException(nameof(OPC_Client_Crane));
         }

         lock (_eOfenSyncRoot)
         {
            ResetEOfenSubscription();

            _logger.LogInformation("0520|Verbindung zur E-Ofen-SPS ueber {OpcServerEndpoint} wird aufgebaut.", opcServerEndpoint);
            eOfenClient?.Connect();

            eOfenSubscription = eOfenClient?.SubscribeNodes();
            _runtimeStatus.SetOpcEOfenSpsStatus(true, "Verbunden");

            _logger.LogInformation("0521|E-Ofen-OPC-Verbindung ist bereit.");
         }
      }

      private void RunStartupOpcNodeReadCheck()
      {
         if (client is null)
         {
            _logger.LogWarning("0180|OPC-Startcheck uebersprungen: OPC-Client ist nicht initialisiert.");
            return;
         }

         IReadOnlyList<(int EventId, string EventName, string Direction, string Partner, string NodeName, string NodeRole, string OpcNode)> nodes;

         try
         {
            nodes = LoadActiveEventOpcNodesForStartupCheck();
         }
         catch (Exception ex)
         {
            _logger.LogWarning(ex, "0180|OPC-Startcheck uebersprungen: Event-Node-Konfiguration konnte nicht aus der Datenbank gelesen werden.");
            return;
         }

         if (nodes.Count == 0)
         {
            _logger.LogWarning("0180|OPC-Startcheck uebersprungen: Keine aktiven Event-Nodes in der Datenbank gefunden.");
            return;
         }

         int okCount = 0;
         int failedCount = 0;
         var failedOpcNodes = new List<string>();

         _logger.LogInformation("0180|OPC-Startcheck gestartet: {Count} aktive Event-Nodes werden testweise gelesen.", nodes.Count);

         foreach ((int eventId, string eventName, string direction, string partner, string nodeName, string nodeRole, string opcNode) in nodes)
         {
            if (!IsConfiguredOpcNode(opcNode))
            {
               failedCount++;
               failedOpcNodes.Add(string.IsNullOrWhiteSpace(opcNode) ? "<LEER>" : opcNode);
               _logger.LogError(
                  "0182|OPC-Startcheck FEHLER: EventID={EventId}, Event={Event}, Direction={Direction}, Partner={Partner}, NodeName={NodeName}, Rolle={Role}, OPC_Node ist leer oder ungueltig.",
                  eventId,
                  eventName,
                  direction,
                  partner,
                  nodeName,
                  nodeRole);
               continue;
            }

            try
            {
               OpcValue value = client.ReadNode(opcNode);
               if (value.Status.IsGood)
               {
                  okCount++;
                  _logger.LogDebug(
                     "0183|OPC-Startcheck OK: EventID={EventId}, Event={Event}, Direction={Direction}, Partner={Partner}, NodeName={NodeName}, Rolle={Role}, Node={Node}, {Diagnose}.",
                     eventId,
                     eventName,
                     direction,
                     partner,
                     nodeName,
                     nodeRole,
                     opcNode,
                     DescribeOpcValue(value));
                  continue;
               }

               failedCount++;
               failedOpcNodes.Add(opcNode);
               _logger.LogError(
                  "0182|OPC-Startcheck FEHLER: EventID={EventId}, Event={Event}, Direction={Direction}, Partner={Partner}, NodeName={NodeName}, Rolle={Role}, Node={Node}, {Diagnose}.",
                  eventId,
                  eventName,
                  direction,
                  partner,
                  nodeName,
                  nodeRole,
                  opcNode,
                  DescribeOpcValue(value));
            }
            catch (Exception ex)
            {
               failedCount++;
               failedOpcNodes.Add(opcNode);
               _logger.LogError(
                  ex,
                  "0182|OPC-Startcheck EXCEPTION: EventID={EventId}, Event={Event}, Direction={Direction}, Partner={Partner}, NodeName={NodeName}, Rolle={Role}, Node={Node}.",
                  eventId,
                  eventName,
                  direction,
                  partner,
                  nodeName,
                  nodeRole,
                  opcNode);
            }
         }

         if (failedCount == 0)
         {
            _logger.LogInformation(
               "0181|OPC-Startcheck abgeschlossen: Alle {OkCount} aktiven Event-Nodes sind lesbar.",
               okCount);
            return;
         }

         _logger.LogError(
            "0181|OPC-Startcheck abgeschlossen: Lesbar={OkCount}, Fehler={FailedCount}, Gesamt={TotalCount}. Details siehe Logzeilen 0182.",
            okCount,
            failedCount,
            nodes.Count);

         _logger.LogError(
            "\n{FailedOpcNodes}",
            string.Join(Environment.NewLine, failedOpcNodes.Distinct(StringComparer.OrdinalIgnoreCase)));
      }

      private IReadOnlyList<(int EventId, string EventName, string Direction, string Partner, string NodeName, string NodeRole, string OpcNode)> LoadActiveEventOpcNodesForStartupCheck()
      {
         var nodes = new List<(int EventId, string EventName, string Direction, string Partner, string NodeName, string NodeRole, string OpcNode)>();

         using SqlConnection connection = new(_configManager.ConnectionString);
         using SqlCommand command = new(
            """
            SELECT
               e.ID AS EventID,
               e.EventName,
               e.Direction,
               ISNULL(e.Partner, N'') AS Partner,
               n.NodeName,
               n.NodeRole,
               n.OPC_Node
            FROM dbo.FALCOM_EVENTS e
            INNER JOIN dbo.FALCOM_EVENT_OPC_NODES n
               ON n.EventID = e.ID
            WHERE ISNULL(e.IsActive, 1) = 1
            ORDER BY e.ID, n.ID
            """,
            connection);

         command.CommandType = CommandType.Text;
         command.CommandTimeout = 30;

         connection.Open();
         using SqlDataReader reader = command.ExecuteReader();

         while (reader.Read())
         {
            nodes.Add((
               Convert.ToInt32(reader["EventID"], CultureInfo.InvariantCulture),
               Convert.ToString(reader["EventName"], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
               Convert.ToString(reader["Direction"], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
               Convert.ToString(reader["Partner"], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
               Convert.ToString(reader["NodeName"], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
               Convert.ToString(reader["NodeRole"], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty,
               Convert.ToString(reader["OPC_Node"], CultureInfo.InvariantCulture)?.Trim() ?? string.Empty));
         }

         return nodes;
      }

      private void RecreateSubscription()
      {
         ResetSubscription();
         subscription = client?.SubscribeNodes();
         subscriptionCreatedUtc = DateTime.UtcNow;
         lastEvent201ReceivedUtc = DateTime.MinValue;
         event201WatchdogFaultStartedUtc = DateTime.MinValue;
         nextEvent201WatchdogLogUtc = DateTime.MinValue;

         if (!ConnectChannels())
         {
            throw new InvalidOperationException("OPC-Kanal 'Zaehler' konnte nicht registriert werden.");
         }
      }

      private void RecreateCwSubscription()
      {
         ResetCwSubscription();
         cwSubscription = cwClient?.SubscribeNodes();

         if (!ConnectCwChannels())
         {
            throw new InvalidOperationException("OPC-Kanal 'CW' konnte nicht registriert werden.");
         }
      }

      private bool ConnectCwChannels()
      {
         if (cwSubscription == null || cwClient == null)
         {
            return false;
         }

         AddCwMonitoredTrigger(Event401Name, Event401TriggerNodeName, event401OpcNodesByName, "0317");
         AddCwMonitoredTrigger(Event402Name, Event402TriggerNodeName, event402OpcNodesByName, "0307");
         AddCwMonitoredTrigger(Event403Name, Event403TriggerNodeName, event403OpcNodesByName, "030B");
         AddCwMonitoredTrigger(Event404Name, Event404TriggerNodeName, event404OpcNodesByName, "030F");
         AddCwMonitoredTrigger(Event405Name, Event405TriggerNodeName, event405OpcNodesByName, "0313");

         return cwMonitoredItems.Count > 0;
      }

      private void AddCwMonitoredTrigger(
         string eventName,
         string triggerNodeName,
         IReadOnlyDictionary<string, string> opcNodesByName,
         string logCode)
      {
         if (string.Equals(eventName, Event401Name, StringComparison.OrdinalIgnoreCase))
         {
            event401InitialwertGesehen = false;
            lastEvent401AenderungsZaehler = null;
         }
         else if (string.Equals(eventName, Event402Name, StringComparison.OrdinalIgnoreCase))
         {
            event402InitialwertGesehen = false;
            lastEvent402AenderungsZaehler = null;
         }
         else if (string.Equals(eventName, Event403Name, StringComparison.OrdinalIgnoreCase))
         {
            event403InitialwertGesehen = false;
            lastEvent403AenderungsZaehler = null;
         }
         else if (string.Equals(eventName, Event404Name, StringComparison.OrdinalIgnoreCase))
         {
            event404InitialwertGesehen = false;
            lastEvent404AenderungsZaehler = null;
         }
         else if (string.Equals(eventName, Event405Name, StringComparison.OrdinalIgnoreCase))
         {
            event405InitialwertGesehen = false;
            lastEvent405AenderungsZaehler = null;
         }

         if (opcNodesByName.TryGetValue(triggerNodeName, out string? triggerNode)
             && IsConfiguredOpcNode(triggerNode))
         {
            var item = new OpcMonitoredItem(triggerNode, OpcAttribute.Value)
            {
               Tag = $"{eventName}.{triggerNodeName}"
            };
            item.DataChangeReceived += HandleDataChange;
            cwSubscription!.AddMonitoredItem(item);
            cwMonitoredItems.Add(item);
         }
         else
         {
            _logger.LogWarning(
               "{LogCode}|{EventName} ist nicht aktiv: Trigger-Node {TriggerNodeName} ist nicht gueltig konfiguriert.",
               logCode,
               eventName,
               triggerNodeName);
         }
      }

      private bool EnsureEvent201SubscriptionDataFlow()
      {
         DateTime nowUtc = DateTime.UtcNow;
         DateTime referenceUtc = lastEvent201ReceivedUtc == DateTime.MinValue
            ? subscriptionCreatedUtc
            : lastEvent201ReceivedUtc;

         if (referenceUtc == DateTime.MinValue)
         {
            return false;
         }

         TimeSpan age = nowUtc - referenceUtc;
         bool initialWait = lastEvent201ReceivedUtc == DateTime.MinValue;
         TimeSpan reconnectTimeout = initialWait
            ? Event201StartupGracePeriod
            : Event201ReconnectTimeout;

         if (age < reconnectTimeout)
         {
            event201WatchdogFaultStartedUtc = DateTime.MinValue;
            return !initialWait;
         }

         if (event201WatchdogFaultStartedUtc == DateTime.MinValue)
         {
            event201WatchdogFaultStartedUtc = nowUtc;
         }

         if (nowUtc >= nextEvent201WatchdogLogUtc)
         {
            _logger.LogError(
               "0061|Event_201 Subscription-Watchdog: Seit {AgeSeconds:F0} Sekunden kein SPS-Lebenszaehler empfangen. Radikaler Reconnect wird gestartet. Node={Node}, LetzterWert={LastValue}, Initialphase={InitialWait}.",
               age.TotalSeconds,
               event201NodeId,
               lastEvent201Value,
               initialWait);
            nextEvent201WatchdogLogUtc = nowUtc.AddSeconds(30);
         }

         MarkOpcDataFlowUnavailable("Reconnect laeuft", "Event_201 bleibt aus");
         throw new InvalidOperationException(
            $"Event_201 wurde seit {age.TotalSeconds:F0} Sekunden nicht empfangen. Radikaler Reconnect wird gestartet.");
      }

      private void MarkOpcDataFlowAvailable(string statusText)
      {
         spsDataUnavailable = false;
         spsLebensZaehlerFreigegeben = true;
         _runtimeStatus.SetOpcKranSpsStatus(true, statusText);
      }

      private void MarkOpcDataFlowChecking(string opcStatusText, string lebensZaehlerStatusText)
      {
         spsDataUnavailable = true;
         spsLebensZaehlerFreigegeben = false;
         _runtimeStatus.SetOpcKranSpsStatus(true, opcStatusText);
         _runtimeStatus.SetSpsLebensZaehlerUnavailable(lebensZaehlerStatusText);
      }

      private void MarkOpcDataFlowUnavailable(string opcStatusText, string lebensZaehlerStatusText)
      {
         spsDataUnavailable = true;
         spsLebensZaehlerFreigegeben = false;
         _runtimeStatus.SetOpcKranSpsStatus(false, opcStatusText);
         _runtimeStatus.SetSpsLebensZaehlerUnavailable(lebensZaehlerStatusText);
      }
      private sealed record KranfahrtAuftragOpcNodes(
         string AuftragNummer,
         string AuftragTeilfahrt,
         string Quelle,
         string Ziel,
         string QuelleUnterposition,
         string ZielUnterposition,
         string SollMasse,
         string MasseTolPos,
         string MasseTolNeg,
         string EventTrigger,
         string MaterialNr);

      public sealed record OpcSendResult(bool Success, string Reason, int? TelegrammNummer = null, int? ZaehlerAnfahrt = null)
      {
         public static OpcSendResult Ok(int? telegrammNummer = null, int? zaehlerAnfahrt = null)
         {
            return new OpcSendResult(true, string.Empty, telegrammNummer, zaehlerAnfahrt);
         }

         public static OpcSendResult Failed(string reason)
         {
            return new OpcSendResult(false, reason);
         }
      }

      public sealed record OpcReadResult<T>(bool Success, T? Value, string Reason)
      {
         public static OpcReadResult<T> Ok(T value)
         {
            return new OpcReadResult<T>(true, value, string.Empty);
         }

         public static OpcReadResult<T> Failed(string reason)
         {
            return new OpcReadResult<T>(false, default, reason);
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
               backgroundReconnectCancellation.Cancel();

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

         DisconnectCwClient();
         DisconnectEOfenClient();
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
            backgroundReconnectCancellation.Cancel();
            backgroundReconnectCancellation.Dispose();
            disposed = true;
         }

         DisposeCwClient();
         DisposeEOfenClient();
      }

      private void DisconnectCwClient()
      {
         lock (_cwSyncRoot)
         {
            try
            {
               ResetCwSubscription();

               if (cwClient is not null)
               {
                  cwClient.StateChanged -= OnCwClientStateChanged;
                  cwClient.Disconnect();
               }
            }
            catch (Exception ex)
            {
               _logger.LogError(ex, "032C|Fehler beim Trennen des CW-OPC-Clients.");
            }
         }
      }

      private void DisconnectEOfenClient()
      {
         lock (_eOfenSyncRoot)
         {
            try
            {
               ResetEOfenSubscription();

               if (eOfenClient is not null)
               {
                  eOfenClient.StateChanged -= OnEOfenClientStateChanged;
                  eOfenClient.Disconnect();
               }
            }
            catch (Exception ex)
            {
               _logger.LogError(ex, "052C|Fehler beim Trennen des E-Ofen-OPC-Clients.");
            }
         }
      }

      private void DisposeCwClient()
      {
         lock (_cwSyncRoot)
         {
            ResetCwSubscription();

            if (cwClient is null)
            {
               return;
            }

            cwClient.StateChanged -= OnCwClientStateChanged;
            cwClient.Dispose();
            cwClient = null;
         }
      }

      private void DisposeEOfenClient()
      {
         lock (_eOfenSyncRoot)
         {
            ResetEOfenSubscription();

            if (eOfenClient is null)
            {
               return;
            }

            eOfenClient.StateChanged -= OnEOfenClientStateChanged;
            eOfenClient.Dispose();
            eOfenClient = null;
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

      private void ResetCwSubscription()
      {
         if (cwSubscription is null)
         {
            cwMonitoredItems.Clear();
            return;
         }

         foreach (OpcMonitoredItem item in cwMonitoredItems)
         {
            item.DataChangeReceived -= HandleDataChange;
         }

         if (cwMonitoredItems.Count > 0)
         {
            try
            {
               cwSubscription.RemoveMonitoredItem(cwMonitoredItems);
               cwSubscription.ApplyChanges();
            }
            catch (Exception ex)
            {
               _logger.LogDebug(ex, "0323|Alte CW-Subscription konnte wegen totem Kanal nicht sauber entfernt werden. Wird erzwungen.");
            }

            cwMonitoredItems.Clear();
         }

         cwSubscription = null;
      }

      private void ResetEOfenSubscription()
      {
         if (eOfenSubscription is null)
         {
            eOfenMonitoredItems.Clear();
            return;
         }

         foreach (OpcMonitoredItem item in eOfenMonitoredItems)
         {
            item.DataChangeReceived -= HandleDataChange;
         }

         if (eOfenMonitoredItems.Count > 0)
         {
            try
            {
               eOfenSubscription.RemoveMonitoredItem(eOfenMonitoredItems);
               eOfenSubscription.ApplyChanges();
            }
            catch (Exception ex)
            {
               _logger.LogDebug(ex, "0523|Alte E-Ofen-Subscription konnte wegen totem Kanal nicht sauber entfernt werden. Wird erzwungen.");
            }

            eOfenMonitoredItems.Clear();
         }

         eOfenSubscription = null;
      }

      public Boolean ConnectChannels()
      {
         if (subscription == null || client == null) return false;

         var zaehlerItem = new OpcMonitoredItem(event201NodeId, OpcAttribute.Value);
         zaehlerItem.DataChangeReceived += HandleDataChange;
         subscription.AddMonitoredItem(zaehlerItem);
         monitoredItems.Add(zaehlerItem);

         kranfahrtBeendetInitialwertGesehen = false;
         lastKranfahrtBeendetAenderungsZaehler = null;
         var kranfahrtBeendetItem = new OpcMonitoredItem(KranfahrtBeendetEvent.AenderungsZaehlerOPCNode, OpcAttribute.Value);
         kranfahrtBeendetItem.DataChangeReceived += HandleDataChange;
         subscription.AddMonitoredItem(kranfahrtBeendetItem);
         monitoredItems.Add(kranfahrtBeendetItem);

         lkwPlatzLeerInitialwertGesehen = false;
         lastLkwPlatzLeerAenderungsZaehler = null;
         var lkwPlatzLeerItem = new OpcMonitoredItem(LkwPlatzLeer207Event.AenderungsZaehlerOPCNode, OpcAttribute.Value)
         {
            Tag = "Event_207.Event_207"
         };
         lkwPlatzLeerItem.DataChangeReceived += HandleDataChange;
         subscription.AddMonitoredItem(lkwPlatzLeerItem);
         monitoredItems.Add(lkwPlatzLeerItem);

         event204InitialwertGesehen = false;
         lastEvent204AnforderungsZaehler = null;
         if (event204OpcNodesByName.TryGetValue(Event204TriggerNodeName, out string? event204TriggerNode)
             && IsConfiguredOpcNode(event204TriggerNode))
         {
            var event204Item = new OpcMonitoredItem(event204TriggerNode, OpcAttribute.Value)
            {
               Tag = "Event_204.Event_204"
            };
            event204Item.DataChangeReceived += HandleDataChange;
            subscription.AddMonitoredItem(event204Item);
            monitoredItems.Add(event204Item);
         }
         else
         {
            _logger.LogWarning(
               "01D9|Event_204 ist nicht aktiv: Trigger-Node Event_204 ist nicht gueltig konfiguriert.");
         }

         event205InitialwertGesehen = false;
         lastEvent205AnforderungsZaehler = null;
         if (event205OpcNodesByName.TryGetValue(Event205TriggerNodeName, out string? event205TriggerNode)
             && IsConfiguredOpcNode(event205TriggerNode))
         {
            var event205Item = new OpcMonitoredItem(event205TriggerNode, OpcAttribute.Value)
            {
               Tag = "Event_205.Event_205"
            };
            event205Item.DataChangeReceived += HandleDataChange;
            subscription.AddMonitoredItem(event205Item);
            monitoredItems.Add(event205Item);
         }
         else
         {
            _logger.LogWarning(
               "0200|Event_205 ist nicht aktiv: Trigger-Node Event_205 ist nicht gueltig konfiguriert.");
         }
         event206InitialwertGesehen = false;
         lastEvent206AnforderungsZaehler = null;
         if (event206OpcNodesByName.TryGetValue(Event206TriggerNodeName, out string? event206TriggerNode)
             && IsConfiguredOpcNode(event206TriggerNode))
         {
            var event206Item = new OpcMonitoredItem(event206TriggerNode, OpcAttribute.Value)
            {
               Tag = "Event_206.Event_206"
            };
            event206Item.DataChangeReceived += HandleDataChange;
            subscription.AddMonitoredItem(event206Item);
            monitoredItems.Add(event206Item);
         }
         else
         {
            _logger.LogWarning(
               "01F0|Event_206 ist nicht aktiv: Trigger-Node Event_206 ist nicht gueltig konfiguriert.");
         }

         if (TryGetConfiguredKranfahrtAuftragLiveNode(Event102TriggerNodeName, out string telegrammNummerNode))
         {
            var kranfahrtAuftragTelegrammItem = new OpcMonitoredItem(telegrammNummerNode, OpcAttribute.Value)
            {
               Tag = "Event_102.Event_102"
            };
            kranfahrtAuftragTelegrammItem.DataChangeReceived += HandleDataChange;
            subscription.AddMonitoredItem(kranfahrtAuftragTelegrammItem);
            monitoredItems.Add(kranfahrtAuftragTelegrammItem);
         }
         else
         {
            _logger.LogWarning("0061|Event_102 Live-Anzeige ist nicht aktiv: Trigger-Node Event_102 ist nicht gueltig konfiguriert.");
         }

         if (event203OpcNodesByName.TryGetValue(Event203TriggerNodeName, out string? kranPositionTriggerNode)
             && IsConfiguredOpcNode(kranPositionTriggerNode)
             && !string.Equals(kranPositionTriggerNode, event201NodeId, StringComparison.Ordinal))
         {
            var positionTriggerItem = new OpcMonitoredItem(kranPositionTriggerNode, OpcAttribute.Value)
            {
               Tag = "Event_203.Event_203"
            };
            positionTriggerItem.DataChangeReceived += HandleDataChange;
            subscription.AddMonitoredItem(positionTriggerItem);
            monitoredItems.Add(positionTriggerItem);
         }

         subscription.ApplyChanges();

         _logger.LogInformation(
            "0021|Kanal 'Zaehler' erfolgreich registriert. Event_201 Node={Node}, MonitoredItems={Count}.",
            event201NodeId,
            monitoredItems.Count);
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
            "004E|Event_201 aktiv. In den letzten 60 Sekunden wurden {EventCount} Event_201-Telegramme empfangen. Aktueller Zaehler={LebensZaehler}.",
            kranSpsLebensZaehlerEventsInCurrentMinute,
            currentLebensZaehler);

         kranSpsLebensZaehlerEventsInCurrentMinute = 0;
         nextKranSpsLebensZaehlerLogUtc = nowUtc.AddMinutes(1);
      }

      private bool IsEvent203TriggerNode(string nodeId)
      {
         return event203OpcNodesByName.TryGetValue(Event203TriggerNodeName, out string? triggerNode)
                && IsConfiguredOpcNode(triggerNode)
                && string.Equals(nodeId, triggerNode, StringComparison.Ordinal);
      }

      private bool IsKranfahrtAuftragTelegrammTriggerNode(string nodeId)
      {
         return kranfahrtAuftragLiveOpcNodesByName.TryGetValue(Event102TriggerNodeName, out string? triggerNode)
                && IsConfiguredOpcNode(triggerNode)
                && string.Equals(nodeId, triggerNode, StringComparison.Ordinal);
      }

      private bool TryGetConfiguredKranfahrtAuftragLiveNode(string nodeName, out string opcNode)
      {
         opcNode = string.Empty;

         if (!kranfahrtAuftragLiveOpcNodesByName.TryGetValue(nodeName, out string? configuredNode)
             || !IsConfiguredOpcNode(configuredNode))
         {
            return false;
         }

         opcNode = configuredNode;
         return true;
      }

      private void TryReadAndSendEvent203FromTrigger(string triggerNodeId, object? triggerValue = null)
      {
         if (!IsEvent203TriggerNode(triggerNodeId))
         {
            return;
         }

         if (!TryGetConfiguredEvent203Node(PosKranNodeName, out _)
             || !TryGetConfiguredEvent203Node(PosKatzeNodeName, out _)
             || !TryGetConfiguredEvent203Node(PosHubNodeName, out _))
         {
            LogEvent203ConfigurationIssueIfDue(
               "Event_203-Trigger empfangen, aber mindestens ein Payload-Node PosKran/PosKatze/PosHub ist nicht gueltig konfiguriert.");
            return;
         }

         try
         {
            Dictionary<string, object?> eventValues = ReadConfiguredEventValues(
               event203OpcNodesByName,
               Event203Name);

            aktuellePosKranX = GetRequiredEventInt32(eventValues, PosKranNodeName);
            aktuellePosKatzeY = GetRequiredEventInt32(eventValues, PosKatzeNodeName);
            aktuellePosHubZ = GetRequiredEventInt32(eventValues, PosHubNodeName);
            aktuellerMagnetAn = GetOptionalEventInt32(eventValues, MagnetAnNodeName);
            aktuelleMasseNetto = GetOptionalEventInt32(eventValues, MasseNettoNodeName);
            event203EventsInCurrentMinute++;

            _ = _kranLiveSignalRClient.SendKranPositionAsync(
               aktuellePosKranX,
               aktuellePosKatzeY,
               aktuellePosHubZ,
               aktuellerMagnetAn,
               aktuelleMasseNetto,
               CancellationToken.None);

            _ = _kranLiveSignalRClient.SendKranOpcEventAsync(
               Event203Id,
               Event203Name,
               Event203Direction,
               Event203TriggerNodeName,
               triggerValue,
               eventValues,
               CancellationToken.None);

            LogEvent203SummaryIfDue();
         }
         catch (Exception ex)
         {
            _logger.LogWarning(
               ex,
               "005C|Event_203 wurde getriggert, aber die Payloads konnten nicht vollstaendig gelesen werden. TriggerNode={TriggerNode}.",
               triggerNodeId);
         }
      }

      private void TryReadAndSendKranfahrtAuftragLiveSnapshot(string triggerNodeId, object? triggerValue)
      {
         if (!IsKranfahrtAuftragTelegrammTriggerNode(triggerNodeId))
         {
            return;
         }

         int telegrammNummer;

         try
         {
            telegrammNummer = Convert.ToInt32(triggerValue);
         }
         catch (Exception ex)
         {
            _logger.LogWarning(
               ex,
               "0062|Event_102-Trigger konnte nicht als Int32 interpretiert werden. Node={Node}, Wert={Value}.",
               triggerNodeId,
               triggerValue);
            return;
         }

         if (lastKranfahrtAuftragTelegrammNummer == telegrammNummer)
         {
            return;
         }

         lastKranfahrtAuftragTelegrammNummer = telegrammNummer;

         try
         {
            Dictionary<string, object?> values = ReadConfiguredEventValues(kranfahrtAuftragLiveOpcNodesByName);

            _ = _kranLiveSignalRClient.SendKranOpcEventAsync(
               KranfahrtAuftragEventId,
               KranfahrtAuftragEventName,
               KranfahrtAuftragDirection,
               Event102TriggerNodeName,
               telegrammNummer,
               values,
               CancellationToken.None);

            _logger.LogInformation(
               "0063|Event_102 Live-Snapshot an Webanwendung vorgemerkt. Triggerwert={TelegrammNummer}.",
               telegrammNummer);
         }
         catch (Exception ex)
         {
            _logger.LogWarning(
               ex,
               "0064|Event_102 Live-Snapshot konnte nach Event_102-Trigger nicht gelesen werden. TriggerNode={TriggerNode}.",
               triggerNodeId);
         }
      }

      private void SendKranfahrtBeendetLiveSnapshot(
         object? aenderungsZaehler,
         int auftragId,
         int teilfahrtID,
         string kranQuelle,
         string kranZiel,
         int status,
         double istGewicht)
      {
         var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
         {
            [KranfahrtBeendetEvent.TriggerNodeName] = aenderungsZaehler,
            [KranfahrtBeendetEvent.AuftragNummerNodeName] = auftragId,
            [KranfahrtBeendetEvent.AuftragTeilfahrtNodeName] = teilfahrtID,
            [KranfahrtBeendetEvent.QuelleNodeName] = kranQuelle,
            [KranfahrtBeendetEvent.ZielNodeName] = kranZiel,
            [KranfahrtBeendetEvent.StatusNodeName] = status,
            [KranfahrtBeendetEvent.IstGewichtNodeName] = istGewicht
         };

         _ = _kranLiveSignalRClient.SendKranOpcEventAsync(
            KranfahrtBeendetEventId,
            KranfahrtBeendetEvent.EventName,
            "KRAN_SPS->FALCOM",
            KranfahrtBeendetEvent.TriggerNodeName,
            aenderungsZaehler,
            values,
            CancellationToken.None);
      }

      private OpcValue ReadRequiredOpcPayloadWithNullRetry(
         string eventName,
         string variable,
         string opcNode)
      {
         return ReadRequiredOpcPayloadWithNullRetry(
            client!,
            eventName,
            variable,
            opcNode);
      }

      private OpcValue ReadRequiredOpcPayloadWithNullRetry(
         OpcClient targetClient,
         string eventName,
         string variable,
         string opcNode)
      {
         OpcValue value = targetClient.ReadNode(opcNode);

         if (!value.Status.IsGood)
         {
            throw new InvalidOperationException(
               $"{eventName}.{variable} ist nicht sauber lesbar. Node={opcNode}, {DescribeOpcValue(value)}");
         }

         if (value.Value is not null)
         {
            return value;
         }

         _logger.LogError(
            "0101|OPC Payloadwert ist NULL. Event={Event}, Variable={Variable}, Node={Node}, {Diagnose}. Warte 500 ms und lese denselben Node erneut.",
            eventName,
            variable,
            opcNode,
            DescribeOpcValue(value));

         System.Threading.Thread.Sleep(500);

         OpcValue retryValue = targetClient.ReadNode(opcNode);

         if (!retryValue.Status.IsGood)
         {
            _logger.LogError(
               "0102|OPC Payloadwert-Reload nach 500 ms fehlgeschlagen. Event={Event}, Variable={Variable}, Node={Node}, {Diagnose}.",
               eventName,
               variable,
               opcNode,
               DescribeOpcValue(retryValue));
            throw new InvalidOperationException(
               $"{eventName}.{variable} ist nach NULL-Retry nicht sauber lesbar. Node={opcNode}, {DescribeOpcValue(retryValue)}");
         }

         if (retryValue.Value is null)
         {
            _logger.LogError(
               "0103|OPC Payloadwert ist auch nach 500 ms NULL. Event={Event}, Variable={Variable}, Node={Node}, {Diagnose}. Verarbeitung wird abgebrochen.",
               eventName,
               variable,
               opcNode,
               DescribeOpcValue(retryValue));
            throw new InvalidOperationException(
               $"OPC Payloadwert ist auch nach 500 ms NULL. Event={eventName}, Variable={variable}, Node={opcNode}");
         }

         _logger.LogInformation(
            "0104|OPC Payloadwert nach NULL-Retry erfolgreich gelesen. Event={Event}, Variable={Variable}, Node={Node}, {Diagnose}.",
            eventName,
            variable,
            opcNode,
            DescribeOpcValue(retryValue));

         return retryValue;
      }

      private static string DescribeOpcValue(OpcValue value)
      {
         return $"Status={value.Status.Code}, Beschreibung={value.Status.Description}, Wert={(value.Value is null ? "<null>" : value.Value)}, DataType={value.DataType}, SourceTimestamp={value.SourceTimestamp:O}, ServerTimestamp={value.ServerTimestamp:O}";
      }
      private Dictionary<string, object?> ReadConfiguredEventValues(
         IReadOnlyDictionary<string, string> opcNodesByName,
         string eventName = "KonfiguriertesOPCEvent")
      {
         return ReadConfiguredEventValues(
            client!,
            opcNodesByName,
            eventName);
      }

      private Dictionary<string, object?> ReadConfiguredEventValues(
         OpcClient targetClient,
         IReadOnlyDictionary<string, string> opcNodesByName,
         string eventName = "KonfiguriertesOPCEvent")
      {
         var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

         if (targetClient is null)
         {
            throw new InvalidOperationException("OPC-Client ist nicht initialisiert. Eventwerte konnten nicht gelesen werden.");
         }

         object? lockTarget = ReferenceEquals(targetClient, client)
            ? _syncRoot
            : null;

         if (lockTarget is not null)
         {
            lock (lockTarget)
            {
               ReadConfiguredEventValuesUnlocked(targetClient, opcNodesByName, eventName, values);
            }
         }
         else
         {
            ReadConfiguredEventValuesUnlocked(targetClient, opcNodesByName, eventName, values);
         }

         return values;
      }

      private void ReadConfiguredEventValuesUnlocked(
         OpcClient targetClient,
         IReadOnlyDictionary<string, string> opcNodesByName,
         string eventName,
         Dictionary<string, object?> values)
      {
         foreach (KeyValuePair<string, string> node in opcNodesByName)
         {
            if (!IsConfiguredOpcNode(node.Value))
            {
               continue;
            }

            OpcValue value = ReadRequiredOpcPayloadWithNullRetry(
               targetClient,
               eventName,
               node.Key,
               node.Value);

            values[node.Key] = value.Value;
         }
      }

      private bool IsEvent204TriggerNode(string nodeId)
      {
         return event204OpcNodesByName.TryGetValue(Event204TriggerNodeName, out string? triggerNode)
                && IsConfiguredOpcNode(triggerNode)
                && string.Equals(nodeId, triggerNode, StringComparison.Ordinal);
      }

      private bool IsEvent205TriggerNode(string nodeId)
      {
         return event205OpcNodesByName.TryGetValue(Event205TriggerNodeName, out string? triggerNode)
                && IsConfiguredOpcNode(triggerNode)
                && string.Equals(nodeId, triggerNode, StringComparison.Ordinal);
      }
      private bool IsEvent206TriggerNode(string nodeId)
      {
         return event206OpcNodesByName.TryGetValue(Event206TriggerNodeName, out string? triggerNode)
                && IsConfiguredOpcNode(triggerNode)
                && string.Equals(nodeId, triggerNode, StringComparison.Ordinal);
      }

      private bool IsEvent401TriggerNode(string nodeId)
      {
         return event401OpcNodesByName.TryGetValue(Event401TriggerNodeName, out string? triggerNode)
                && IsConfiguredOpcNode(triggerNode)
                && string.Equals(nodeId, triggerNode, StringComparison.Ordinal);
      }

      private bool IsEvent402TriggerNode(string nodeId)
      {
         return event402OpcNodesByName.TryGetValue(Event402TriggerNodeName, out string? triggerNode)
                && IsConfiguredOpcNode(triggerNode)
                && string.Equals(nodeId, triggerNode, StringComparison.Ordinal);
      }

      private bool IsEvent403TriggerNode(string nodeId)
      {
         return event403OpcNodesByName.TryGetValue(Event403TriggerNodeName, out string? triggerNode)
                && IsConfiguredOpcNode(triggerNode)
                && string.Equals(nodeId, triggerNode, StringComparison.Ordinal);
      }

      private bool IsEvent404TriggerNode(string nodeId)
      {
         return event404OpcNodesByName.TryGetValue(Event404TriggerNodeName, out string? triggerNode)
                && IsConfiguredOpcNode(triggerNode)
                && string.Equals(nodeId, triggerNode, StringComparison.Ordinal);
      }

      private bool IsEvent405TriggerNode(string nodeId)
      {
         return event405OpcNodesByName.TryGetValue(Event405TriggerNodeName, out string? triggerNode)
                && IsConfiguredOpcNode(triggerNode)
                && string.Equals(nodeId, triggerNode, StringComparison.Ordinal);
      }

      private void HandleEvent204Trigger(int anforderungsZaehler)
      {
         bool istInitialwert = !event204InitialwertGesehen;
         event204InitialwertGesehen = true;

         if (anforderungsZaehler <= 0)
         {
            lastEvent204AnforderungsZaehler = anforderungsZaehler;
            _logger.LogInformation(
               "01DA|Event_204 Trigger ist 0 und wird als leere Anforderung ignoriert.");
            return;
         }

         if (lastEvent204AnforderungsZaehler == anforderungsZaehler)
         {
            return;
         }

         lastEvent204AnforderungsZaehler = anforderungsZaehler;
         if (istInitialwert
             && TryReadEvent104ResponseTrigger(out int responseTrigger)
             && responseTrigger == anforderungsZaehler)
         {
            _logger.LogInformation(
               "01DB|Event_204 Initialwert wurde bereits durch Event_104 beantwortet und wird ignoriert. AnforderungsZaehler={AnforderungsZaehler}.",
               anforderungsZaehler);
            return;
         }

         if (!_eventQueue.Writer.TryWrite(
                new BunkerMaterialAnforderungEvent(anforderungsZaehler, istInitialwert)))
         {
            _logger.LogError(
               "01DC|Event_204 konnte nicht in die Event-Queue geschrieben werden. AnforderungsZaehler={AnforderungsZaehler}.",
               anforderungsZaehler);
            return;
         }

         _logger.LogInformation(
            "01DD|Event_204 eingereiht. AnforderungsZaehler={AnforderungsZaehler}, Initialwert={IstInitialwert}.",
            anforderungsZaehler,
            istInitialwert);
      }

      private bool TryReadEvent104ResponseTrigger(out int triggerValue)
      {
         triggerValue = 0;
         try
         {
            string nodeId = GetRequiredConfiguredEventNode(
               event104OpcNodesByName,
               Event104Name,
               Event104TriggerNodeName);
            OpcValue value = client!.ReadNode(nodeId);
            if (!value.Status.IsGood || value.Value is null)
            {
               return false;
            }

            triggerValue = Convert.ToInt32(value.Value);
            return true;
         }
         catch
         {
            // Ohne lesbaren Antwortzaehler wird die Anfrage idempotent neu beantwortet.
            return false;
         }
      }

      private void HandleEvent205Trigger(int anforderungsZaehler)
      {
         bool istInitialwert = !event205InitialwertGesehen;
         event205InitialwertGesehen = true;

         if (anforderungsZaehler <= 0)
         {
            lastEvent205AnforderungsZaehler = anforderungsZaehler;
            _logger.LogInformation(
               "0201|Event_205 Trigger ist 0 und wird als leere Anforderung ignoriert.");
            return;
         }

         if (lastEvent205AnforderungsZaehler == anforderungsZaehler)
         {
            return;
         }

         lastEvent205AnforderungsZaehler = anforderungsZaehler;
         if (istInitialwert
             && TryReadEvent105ResponseTrigger(out int responseTrigger)
             && responseTrigger == anforderungsZaehler)
         {
            _logger.LogInformation(
               "0202|Event_205 Initialwert wurde bereits durch Event_105 beantwortet und wird ignoriert. AnforderungsZaehler={AnforderungsZaehler}.",
               anforderungsZaehler);
            return;
         }

         if (!_eventQueue.Writer.TryWrite(
                new MaterialEigenschaftenAnforderungEvent(anforderungsZaehler, istInitialwert)))
         {
            _logger.LogError(
               "0203|Event_205 konnte nicht in die Event-Queue geschrieben werden. AnforderungsZaehler={AnforderungsZaehler}.",
               anforderungsZaehler);
            return;
         }

         _logger.LogInformation(
            "0204|Event_205 eingereiht. AnforderungsZaehler={AnforderungsZaehler}, Initialwert={IstInitialwert}.",
            anforderungsZaehler,
            istInitialwert);
      }

      private bool TryReadEvent105ResponseTrigger(out int triggerValue)
      {
         triggerValue = 0;
         try
         {
            string nodeId = GetRequiredConfiguredEventNode(
               event105OpcNodesByName,
               Event105Name,
               Event105TriggerNodeName);
            OpcValue value = client!.ReadNode(nodeId);
            if (!value.Status.IsGood || value.Value is null)
            {
               return false;
            }

            triggerValue = Convert.ToInt32(value.Value);
            return true;
         }
         catch
         {
            // Ohne lesbaren Antwortzaehler wird die Anfrage idempotent neu beantwortet.
            return false;
         }
      }
      private void HandleEvent206Trigger(int anforderungsZaehler)
      {
         bool istInitialwert = !event206InitialwertGesehen;
         event206InitialwertGesehen = true;

         if (anforderungsZaehler <= 0)
         {
            lastEvent206AnforderungsZaehler = anforderungsZaehler;
            _logger.LogInformation(
               "01F1|Event_206 Trigger ist 0 und wird als leere Anforderung ignoriert.");
            return;
         }

         if (lastEvent206AnforderungsZaehler == anforderungsZaehler)
         {
            return;
         }

         lastEvent206AnforderungsZaehler = anforderungsZaehler;
         if (istInitialwert
             && TryReadEvent106ResponseTrigger(out int responseTrigger)
             && responseTrigger == anforderungsZaehler)
         {
            _logger.LogInformation(
               "01F2|Event_206 Initialwert wurde bereits durch Event_106 beantwortet und wird ignoriert. AnforderungsZaehler={AnforderungsZaehler}.",
               anforderungsZaehler);
            return;
         }

         if (!_eventQueue.Writer.TryWrite(
                new KranPositionenAnforderungEvent(anforderungsZaehler, istInitialwert)))
         {
            _logger.LogError(
               "01F3|Event_206 konnte nicht in die Event-Queue geschrieben werden. AnforderungsZaehler={AnforderungsZaehler}.",
               anforderungsZaehler);
            return;
         }

         _logger.LogInformation(
            "01F9|Event_206 eingereiht. AnforderungsZaehler={AnforderungsZaehler}, Initialwert={IstInitialwert}.",
            anforderungsZaehler,
            istInitialwert);
      }

      private bool TryReadEvent106ResponseTrigger(out int triggerValue)
      {
         triggerValue = 0;
         try
         {
            string nodeId = GetRequiredConfiguredEventNode(
               event106OpcNodesByName,
               Event106Name,
               Event106TriggerNodeName);
            OpcValue value = client!.ReadNode(nodeId);
            if (!value.Status.IsGood || value.Value is null)
            {
               return false;
            }

            triggerValue = Convert.ToInt32(value.Value);
            return true;
         }
         catch
         {
            // Ohne lesbaren Antwortzaehler wird die Anfrage idempotent neu beantwortet.
            return false;
         }
      }

      private void HandleEvent402Trigger(int aenderungsZaehler)
      {
         bool istInitialwert = !event402InitialwertGesehen;
         event402InitialwertGesehen = true;

         if (aenderungsZaehler <= 0)
         {
            _logger.LogInformation(
               "0308|Event_402 Trigger ist 0. Lese trotzdem die aktuelle CW-Status-Payload fuer die Produktionsuebersicht.");
         }

         if (lastEvent402AenderungsZaehler == aenderungsZaehler)
         {
            return;
         }

         lastEvent402AenderungsZaehler = aenderungsZaehler;

         try
         {
            _runtimeStatus.SetCwDataReceived("Event_402");
            Dictionary<string, object?> values;
            lock (_cwSyncRoot)
            {
               values = ReadConfiguredEventValues(
                  cwClient!,
                  event402OpcNodesByName,
                  Event402Name);
            }

            values.TryGetValue("Istgew_ChW1", out object? istgewChW1);
            values.TryGetValue("Istgew_ChW2", out object? istgewChW2);
            values.TryGetValue("Istgew_ChW3", out object? istgewChW3);

            _ = _kranLiveSignalRClient.SendCwIstgewichteAsync(
               ConvertToNullableInt32(istgewChW1),
               ConvertToNullableInt32(istgewChW2),
               ConvertToNullableInt32(istgewChW3),
               CancellationToken.None);

            _logger.LogInformation(
               "0309|Event_402 empfangen und Payload gelesen: AenderungsZaehler={AenderungsZaehler}, Initialwert={IstInitialwert}, Istgew_ChW1={IstgewChW1}, Istgew_ChW2={IstgewChW2}, Istgew_ChW3={IstgewChW3}.",
               aenderungsZaehler,
               istInitialwert,
               istgewChW1,
               istgewChW2,
               istgewChW3);
         }
         catch (Exception ex)
         {
            _logger.LogWarning(
               ex,
               "030A|Event_402 wurde getriggert, Payload konnte aber nicht gelesen werden. AenderungsZaehler={AenderungsZaehler}.",
               aenderungsZaehler);
         }
      }

      private void HandleEvent401Trigger(int aenderungsZaehler)
      {
         bool istInitialwert = !event401InitialwertGesehen;
         event401InitialwertGesehen = true;

         if (aenderungsZaehler <= 0)
         {
            lastEvent401AenderungsZaehler = aenderungsZaehler;
            _runtimeStatus.SetCwLebensZaehlerUnavailable("Event_401 Trigger ist 0");
            _logger.LogInformation(
               "0318|Event_401 Trigger ist 0 und wird als leeres CW-Lebenszeichen ignoriert.");
            return;
         }

         if (lastEvent401AenderungsZaehler == aenderungsZaehler)
         {
            return;
         }

         lastEvent401AenderungsZaehler = aenderungsZaehler;

         _runtimeStatus.SetCwLebensZaehlerReceived(aenderungsZaehler);

         _logger.LogInformation(
            "0319|Event_401 CW-Lebenszeichen empfangen: Lebenszaehler={Lebenszaehler}, Initialwert={IstInitialwert}.",
            aenderungsZaehler,
            istInitialwert);
      }

      private void SetCwPartnerDataReceived(string statusText)
      {
         _runtimeStatus.SetCwDataReceived(statusText);
      }

      private void HandleEvent403Trigger(int aenderungsZaehler)
      {
         bool istInitialwert = !event403InitialwertGesehen;
         event403InitialwertGesehen = true;

         if (aenderungsZaehler <= 0)
         {
            lastEvent403AenderungsZaehler = aenderungsZaehler;
            _logger.LogInformation(
               "030C|Event_403 Trigger ist 0 und wird als leere Beladebereit-Meldung ignoriert.");
            return;
         }

         if (lastEvent403AenderungsZaehler == aenderungsZaehler)
         {
            return;
         }

         lastEvent403AenderungsZaehler = aenderungsZaehler;

         try
         {
            SetCwPartnerDataReceived("Event_403");
            Dictionary<string, object?> values;
            lock (_cwSyncRoot)
            {
               values = ReadConfiguredEventValues(
                  cwClient!,
                  event403OpcNodesByName,
                  Event403Name);
            }

            values.TryGetValue("Beladebereit_ChW1", out object? beladebereitChW1);
            values.TryGetValue("Beladebereit_ChW2", out object? beladebereitChW2);
            values.TryGetValue("Beladebereit_ChW3", out object? beladebereitChW3);

            _logger.LogInformation(
               "030D|Event_403 empfangen und Payload gelesen: AenderungsZaehler={AenderungsZaehler}, Initialwert={IstInitialwert}, Beladebereit_ChW1={BeladebereitChW1}, Beladebereit_ChW2={BeladebereitChW2}, Beladebereit_ChW3={BeladebereitChW3}.",
               aenderungsZaehler,
               istInitialwert,
               beladebereitChW1,
               beladebereitChW2,
               beladebereitChW3);
         }
         catch (Exception ex)
         {
            _logger.LogWarning(
               ex,
               "030E|Event_403 wurde getriggert, Payload konnte aber nicht gelesen werden. AenderungsZaehler={AenderungsZaehler}.",
               aenderungsZaehler);
         }
      }

      private void HandleEvent404Trigger(int aenderungsZaehler)
      {
         bool istInitialwert = !event404InitialwertGesehen;
         event404InitialwertGesehen = true;

         if (aenderungsZaehler <= 0)
         {
            _logger.LogInformation(
               "0310|Event_404 Trigger ist 0. Lese trotzdem die aktuelle CW-Stoerungs-Payload fuer die Produktionsuebersicht.");
         }

         if (lastEvent404AenderungsZaehler == aenderungsZaehler)
         {
            return;
         }

         lastEvent404AenderungsZaehler = aenderungsZaehler;

         try
         {
            SetCwPartnerDataReceived("Event_404");
            Dictionary<string, object?> values;
            lock (_cwSyncRoot)
            {
               values = ReadConfiguredEventValues(
                  cwClient!,
                  event404OpcNodesByName,
                  Event404Name);
            }

            values.TryGetValue("Stoerung_ChW1", out object? stoerungChW1);
            values.TryGetValue("Stoerung_ChW2", out object? stoerungChW2);
            values.TryGetValue("Stoerung_ChW3", out object? stoerungChW3);

            _ = _kranLiveSignalRClient.SendCwStoerungenAsync(
               ConvertToNullableBoolean(stoerungChW1),
               ConvertToNullableBoolean(stoerungChW2),
               ConvertToNullableBoolean(stoerungChW3),
               CancellationToken.None);

            _logger.LogInformation(
               "0311|Event_404 empfangen und Payload gelesen: AenderungsZaehler={AenderungsZaehler}, Initialwert={IstInitialwert}, Stoerung_ChW1={StoerungChW1}, Stoerung_ChW2={StoerungChW2}, Stoerung_ChW3={StoerungChW3}.",
               aenderungsZaehler,
               istInitialwert,
               stoerungChW1,
               stoerungChW2,
               stoerungChW3);
         }
         catch (Exception ex)
         {
            _logger.LogWarning(
               ex,
               "0312|Event_404 wurde getriggert, Payload konnte aber nicht gelesen werden. AenderungsZaehler={AenderungsZaehler}.",
               aenderungsZaehler);
         }
      }

      private void HandleEvent405Trigger(int aenderungsZaehler)
      {
         bool istInitialwert = !event405InitialwertGesehen;
         event405InitialwertGesehen = true;

         if (aenderungsZaehler <= 0)
         {
            lastEvent405AenderungsZaehler = aenderungsZaehler;
            _logger.LogInformation(
               "0314|Event_405 Trigger ist 0 und wird als leere GattierungAbgeschlossen-Meldung ignoriert.");
            return;
         }

         if (lastEvent405AenderungsZaehler == aenderungsZaehler)
         {
            return;
         }

         lastEvent405AenderungsZaehler = aenderungsZaehler;

         try
         {
            SetCwPartnerDataReceived("Event_405");
            Dictionary<string, object?> values;
            lock (_cwSyncRoot)
            {
               values = ReadConfiguredEventValues(
                  cwClient!,
                  event405OpcNodesByName,
                  Event405Name);
            }

            values.TryGetValue("GattierungAbgeschl", out object? gattierungAbgeschl);
            values.TryGetValue("C", out object? c);
            values.TryGetValue("Si", out object? si);
            values.TryGetValue("MN", out object? mn);
            values.TryGetValue("Cu", out object? cu);
            values.TryGetValue("ChW_ID", out object? chwId);
            values.TryGetValue("AuftragsNr", out object? auftragsNr);

            _logger.LogInformation(
               "0315|Event_405 empfangen und Payload gelesen: AenderungsZaehler={AenderungsZaehler}, Initialwert={IstInitialwert}, GattierungAbgeschl={GattierungAbgeschl}, C={C}, Si={Si}, MN={MN}, Cu={Cu}, ChW_ID={ChWID}, AuftragsNr={AuftragsNr}.",
               aenderungsZaehler,
               istInitialwert,
               gattierungAbgeschl,
               c,
               si,
               mn,
               cu,
               chwId,
               auftragsNr);
         }
         catch (Exception ex)
         {
            _logger.LogWarning(
               ex,
               "0316|Event_405 wurde getriggert, Payload konnte aber nicht gelesen werden. AenderungsZaehler={AenderungsZaehler}.",
               aenderungsZaehler);
         }
      }

      private void EnsureKranfahrtAuftragZaehlerInitialisiert(
         KranfahrtAuftragOpcNodes nodes)
      {
         if (kranfahrtAuftragZaehlerInitialisiert)
         {
            return;
         }

         OpcValue triggerValue = ReadRequiredOpcPayloadWithNullRetry(
            KranfahrtAuftragEvent.EventName,
            KranfahrtAuftragEvent.EventTriggerNodeName,
            nodes.EventTrigger);

         kranfahrtAuftragTelegrammNummer = Convert.ToInt32(
            triggerValue.Value,
            CultureInfo.InvariantCulture);
         kranfahrtAuftragZaehlerInitialisiert = true;

         _logger.LogInformation(
            "0154|Event_102-Zaehler aus OPC synchronisiert. Event_102={Event102}.",
            kranfahrtAuftragTelegrammNummer);
      }

      private static int GetRequiredEventInt32(
         IReadOnlyDictionary<string, object?> values,
         string nodeName)
      {
         if (!values.TryGetValue(nodeName, out object? value) || value is null)
         {
            throw new InvalidOperationException($"Event_203.{nodeName} fehlt im gelesenen Payload.");
         }

         return Convert.ToInt32(value);
      }

      private static int? GetOptionalEventInt32(
         IReadOnlyDictionary<string, object?> values,
         string nodeName)
      {
         return values.TryGetValue(nodeName, out object? value) && value is not null
            ? Convert.ToInt32(value)
            : null;
      }

      private bool TryGetConfiguredEvent203Node(string nodeName, out string opcNode)
      {
         opcNode = string.Empty;

         if (!event203OpcNodesByName.TryGetValue(nodeName, out string? configuredNode)
             || !IsConfiguredOpcNode(configuredNode))
         {
            return false;
         }

         opcNode = configuredNode;
         return true;
      }

      private void LogEvent203ConfigurationIssueIfDue(string reason)
      {
         DateTime nowUtc = DateTime.UtcNow;

         if (nowUtc < nextEvent203ConfigurationLogUtc)
         {
            return;
         }

         _logger.LogWarning(
            "005B|Event_203-Konfiguration ist nicht lesebereit: {Reason}",
            reason);

         nextEvent203ConfigurationLogUtc = nowUtc.AddMinutes(1);
      }

      private void LogEvent203SummaryIfDue()
      {
         DateTime nowUtc = DateTime.UtcNow;

         if (nowUtc < nextEvent203LogUtc)
         {
            return;
         }

         _logger.LogInformation(
            "005A|Event_203 aktiv. In den letzten 60 Sekunden wurden {EventCount} Status-Telegramme empfangen. PosKran={PosKran}, PosKatze={PosKatze}, PosHub={PosHub}, MagnetAn={MagnetAn}, MasseNetto={MasseNetto}.",
            event203EventsInCurrentMinute,
            aktuellePosKranX,
            aktuellePosKatzeY,
            aktuellePosHubZ,
            aktuellerMagnetAn,
            aktuelleMasseNetto);

         event203EventsInCurrentMinute = 0;
         nextEvent203LogUtc = nowUtc.AddMinutes(1);
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
               event201NodeId,
               StringComparison.Ordinal))
            {
               int lebensZaehler = Convert.ToInt32(neuerZaehlerWert);
               lastEvent201ReceivedUtc = DateTime.UtcNow;
               lastEvent201Value = lebensZaehler;
               event201WatchdogFaultStartedUtc = DateTime.MinValue;

               if (!spsLebensZaehlerFreigegeben)
               {
                  _logger.LogInformation(
                     "0057|Event_201 empfangen. OPC-Datenfluss wird wieder freigegeben. Node={Node}, Wert={Value}",
                     e.MonitoredItem.NodeId,
                     lebensZaehler);
               }

               MarkOpcDataFlowAvailable("Verbunden");

               var lebensZaehlerEvent = new KranSpsLebensZaehlerEvent(lebensZaehler);

               if (!_eventQueue.Writer.TryWrite(lebensZaehlerEvent))
               {
                  _logger.LogError("004F|Event_201 konnte nicht in die Event-Queue geschrieben werden.");
                  return;
               }

               kranSpsLebensZaehlerEventsInCurrentMinute++;
               _runtimeStatus.SetSpsLebensZaehlerReceived(lebensZaehler);
               _ = _kranLiveSignalRClient.SendSpsLebensZaehlerAsync(
                  lebensZaehler,
                  CancellationToken.None);
               LogKranSpsLebensZaehlerSummaryIfDue(lebensZaehler);
               return;
            }
            if (IsEvent203TriggerNode(changedNodeId))
            {
               TryReadAndSendEvent203FromTrigger(changedNodeId, neuerZaehlerWert);
               return;
            }
            if (IsEvent204TriggerNode(changedNodeId))
            {
               HandleEvent204Trigger(Convert.ToInt32(neuerZaehlerWert));
               return;
            }
            if (IsEvent205TriggerNode(changedNodeId))
            {
               HandleEvent205Trigger(Convert.ToInt32(neuerZaehlerWert));
               return;
            }
            if (IsEvent206TriggerNode(changedNodeId))
            {
               HandleEvent206Trigger(Convert.ToInt32(neuerZaehlerWert));
               return;
            }
            if (IsEvent401TriggerNode(changedNodeId))
            {
               HandleEvent401Trigger(Convert.ToInt32(neuerZaehlerWert));
               return;
            }
            if (IsEvent402TriggerNode(changedNodeId))
            {
               HandleEvent402Trigger(Convert.ToInt32(neuerZaehlerWert));
               return;
            }
            if (IsEvent403TriggerNode(changedNodeId))
            {
               HandleEvent403Trigger(Convert.ToInt32(neuerZaehlerWert));
               return;
            }
            if (IsEvent404TriggerNode(changedNodeId))
            {
               HandleEvent404Trigger(Convert.ToInt32(neuerZaehlerWert));
               return;
            }
            if (IsEvent405TriggerNode(changedNodeId))
            {
               HandleEvent405Trigger(Convert.ToInt32(neuerZaehlerWert));
               return;
            }

            if (IsKranfahrtAuftragTelegrammTriggerNode(changedNodeId))
            {
               TryReadAndSendKranfahrtAuftragLiveSnapshot(changedNodeId, neuerZaehlerWert);
               return;
            }

            if (string.Equals(
               changedNodeId,
               LkwPlatzLeer207Event.AenderungsZaehlerOPCNode,
               StringComparison.Ordinal))
            {
               int aenderungsZaehler = Convert.ToInt32(neuerZaehlerWert);

               if (!lkwPlatzLeerInitialwertGesehen)
               {
                  lkwPlatzLeerInitialwertGesehen = true;
                  lastLkwPlatzLeerAenderungsZaehler = aenderungsZaehler;
                  if (aenderungsZaehler <= 0)
                  {
                     _logger.LogInformation(
                        "01BD|Event_207 Initialtrigger ist 0 und wird ohne Payload-Lesen ignoriert.");
                     return;
                  }

                  LkwPlatzLeerPayload initialPayload = ReadLkwPlatzLeerPayload(aenderungsZaehler);
                  if (initialPayload.AuftragID <= 0
                      || initialPayload.TeilfahrtID <= 0
                      || initialPayload.LkwPlatzPositionID <= 0)
                  {
                     _logger.LogInformation(
                        "01BD|Event_207 Initialwert ist leer und wird ignoriert: Auftrag={AuftragID}, Teilfahrt={TeilfahrtID}, LkwPlatz={LkwPlatz}, AenderungsZaehler={AenderungsZaehler}.",
                        initialPayload.AuftragID,
                        initialPayload.TeilfahrtID,
                        initialPayload.LkwPlatzPositionID,
                        initialPayload.AenderungsZaehler);
                     return;
                  }

                  QueueLkwPlatzLeerEvent(initialPayload, true);
                  return;
               }

               if (lastLkwPlatzLeerAenderungsZaehler == aenderungsZaehler)
               {
                  return;
               }

               lastLkwPlatzLeerAenderungsZaehler = aenderungsZaehler;
               QueueLkwPlatzLeerEvent(ReadLkwPlatzLeerPayload(aenderungsZaehler), false);
               return;
            }

            if (string.Equals(
               e.MonitoredItem.NodeId.ToString(),
               KranfahrtBeendetEvent.AenderungsZaehlerOPCNode,
               StringComparison.Ordinal))
            {
               int aenderungsZaehler = Convert.ToInt32(e.Item.Value.Value);

               if (!kranfahrtBeendetInitialwertGesehen)
               {
                  kranfahrtBeendetInitialwertGesehen = true;
                  lastKranfahrtBeendetAenderungsZaehler = aenderungsZaehler;

                  KranfahrtBeendetPayload initialPayload = ReadKranfahrtBeendetPayload(aenderungsZaehler);
                  if (!SollKranfahrtBeendetInitialwertVerarbeitetWerden(initialPayload))
                  {
                     _logger.LogInformation(
                        "0025|OPC-Initialwert ignoriert: Rueckmeldung gehoert nicht zur aktuell offenen Fahrt. AenderungsZaehler={AenderungsZaehler}, EmpfangenerAuftrag={AuftragId}, EmpfangeneTeilfahrt={TeilfahrtID}",
                        aenderungsZaehler,
                        initialPayload.AuftragId,
                        initialPayload.TeilfahrtID);
                     return;
                  }

                  _logger.LogInformation(
                     "0026|KranfahrtBeendet Initialwert passt zur aktuellen Fahrt und wird verarbeitet. AenderungsZaehler={AenderungsZaehler}, Auftrag={AuftragId}, Teilfahrt={TeilfahrtID}",
                     aenderungsZaehler,
                     initialPayload.AuftragId,
                     initialPayload.TeilfahrtID);
                  QueueKranfahrtBeendetEvent(initialPayload);
                  return;
               }

               if (lastKranfahrtBeendetAenderungsZaehler == aenderungsZaehler)
               {
                  _logger.LogDebug(
                     "0105|KranfahrtBeendet AenderungsZaehler unveraendert. Wert={AenderungsZaehler}",
                     aenderungsZaehler);
                  return;
               }

               lastKranfahrtBeendetAenderungsZaehler = aenderungsZaehler;

               _logger.LogInformation("0106|Kranfahrt beendet. AenderungsZaehler={AenderungsZaehler}", aenderungsZaehler);
               QueueKranfahrtBeendetEvent(ReadKranfahrtBeendetPayload(aenderungsZaehler));
            }
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, "0029|Fehler im HandleDataChange beim Nachlesen der SPS-Daten.");
         }
      }

      private object ReadRequiredKranfahrtBeendetPayloadValue(string variable, string opcNode)
      {
         OpcValue value = ReadRequiredOpcPayloadWithNullRetry(
            "KranfahrtBeendet",
            variable,
            opcNode);

         return value.Value;
      }

      private LkwPlatzLeerPayload ReadLkwPlatzLeerPayload(int aenderungsZaehler)
      {
         object auftragValue = ReadRequiredOpcPayloadWithNullRetry(
            LkwPlatzLeer207Event.EventName,
            LkwPlatzLeer207Event.AuftragNummerNodeName,
            LkwPlatzLeer207Event.AuftragsNummerOPCNode).Value;
         object teilfahrtValue = ReadRequiredOpcPayloadWithNullRetry(
            LkwPlatzLeer207Event.EventName,
            LkwPlatzLeer207Event.AuftragTeilfahrtNodeName,
            LkwPlatzLeer207Event.TeilfahrtIDOPCNode).Value;
         object lkwPlatzValue = ReadRequiredOpcPayloadWithNullRetry(
            LkwPlatzLeer207Event.EventName,
            LkwPlatzLeer207Event.LkwPlatzNodeName,
            LkwPlatzLeer207Event.LkwPlatzOPCNode).Value;

         var payload = new LkwPlatzLeerPayload(
            Convert.ToInt32(auftragValue),
            Convert.ToInt32(teilfahrtValue),
            Convert.ToInt32(lkwPlatzValue),
            aenderungsZaehler);

         _logger.LogInformation(
            "01B0|Event_207 Payload aus OPC gelesen: Auftrag={AuftragID}, Teilfahrt={TeilfahrtID}, LkwPlatz={LkwPlatz}, AenderungsZaehler={AenderungsZaehler}.",
            payload.AuftragID,
            payload.TeilfahrtID,
            payload.LkwPlatzPositionID,
            payload.AenderungsZaehler);

         return payload;
      }

      private void QueueLkwPlatzLeerEvent(LkwPlatzLeerPayload payload, bool istInitialwert)
      {
         var falcomEvent = new LkwPlatzLeer207Event(
            payload.AuftragID,
            payload.TeilfahrtID,
            payload.LkwPlatzPositionID,
            payload.AenderungsZaehler);

         if (!_eventQueue.Writer.TryWrite(falcomEvent))
         {
            _logger.LogError(
               "01B1|Event_207 konnte nicht in die Event-Queue geschrieben werden. Auftrag={AuftragID}, Teilfahrt={TeilfahrtID}, LkwPlatz={LkwPlatz}.",
               payload.AuftragID,
               payload.TeilfahrtID,
               payload.LkwPlatzPositionID);
            return;
         }
         _logger.LogInformation(
            "01B2|Event_207 eingereiht: Auftrag={AuftragID}, Teilfahrt={TeilfahrtID}, LkwPlatz={LkwPlatz}, AenderungsZaehler={AenderungsZaehler}, Initialwert={IstInitialwert}.",
            payload.AuftragID,
            payload.TeilfahrtID,
            payload.LkwPlatzPositionID,
            payload.AenderungsZaehler,
            istInitialwert);
      }
      private KranfahrtBeendetPayload ReadKranfahrtBeendetPayload(int aenderungsZaehler)
      {
         object auftragIdValue = ReadRequiredKranfahrtBeendetPayloadValue(KranfahrtBeendetEvent.AuftragNummerNodeName, KranfahrtBeendetEvent.AuftragsNummerOPCNode);
         object teilfahrtValue = ReadRequiredKranfahrtBeendetPayloadValue(KranfahrtBeendetEvent.AuftragTeilfahrtNodeName, KranfahrtBeendetEvent.TeilfahrtIDOPCNode);
         object quelleValue = ReadRequiredKranfahrtBeendetPayloadValue(KranfahrtBeendetEvent.QuelleNodeName, KranfahrtBeendetEvent.KranQuelleOPCNode);
         object zielValue = ReadRequiredKranfahrtBeendetPayloadValue(KranfahrtBeendetEvent.ZielNodeName, KranfahrtBeendetEvent.KranZielOPCNode);
         object statusValue = ReadRequiredKranfahrtBeendetPayloadValue(KranfahrtBeendetEvent.StatusNodeName, KranfahrtBeendetEvent.StatusOPCNode);
         object istGewichtValue = ReadRequiredKranfahrtBeendetPayloadValue(KranfahrtBeendetEvent.IstGewichtNodeName, KranfahrtBeendetEvent.IstGewichtOPCNode);

         int auftragId = Convert.ToInt32(auftragIdValue);
         int teilfahrtID = Convert.ToInt32(teilfahrtValue);
         string kranQuelle = Convert.ToString(quelleValue) ?? string.Empty;
         string kranZiel = Convert.ToString(zielValue) ?? string.Empty;
         int status = Convert.ToInt32(statusValue);
         double istGewicht = Convert.ToDouble(istGewichtValue);

         _logger.LogInformation(
            "0085|KranfahrtBeendet Payload aus OPC gelesen: Auftrag={AuftragId}, Teilfahrt={TeilfahrtID}, Quelle={Quelle}, Ziel={Ziel}, Status={Status}, IstGewicht={IstGewicht}, AenderungsZaehler={AenderungsZaehler}.",
            auftragId,
            teilfahrtID,
            kranQuelle,
            kranZiel,
            status,
            istGewicht,
            aenderungsZaehler);

         return new KranfahrtBeendetPayload(
            auftragId,
            teilfahrtID,
            kranQuelle,
            kranZiel,
            status,
            istGewicht,
            aenderungsZaehler);
      }

      private bool SollKranfahrtBeendetInitialwertVerarbeitetWerden(KranfahrtBeendetPayload payload)
      {
         AktuelleFahrtResult aktuelleFahrt = _aktuelleFahrtRepository.GetAktuelleFahrt();
         if (!aktuelleFahrt.Success || aktuelleFahrt.AktuelleFahrtID is null)
         {
            _logger.LogInformation(
               "0107|OPC-Initialwert ignoriert: Es ist keine aktuell offene Fahrt vorhanden. Grund={Reason}",
               aktuelleFahrt.Reason);
            return false;
         }

         int erwarteteTeilfahrt = aktuelleFahrt.AuftragTeilfahrt ?? 1;
         bool passt = aktuelleFahrt.AuftragID == payload.AuftragId
                      && erwarteteTeilfahrt == payload.TeilfahrtID;

         if (!passt)
         {
            _logger.LogInformation(
               "0108|OPC-Initialwert ignoriert: Rueckmeldung gehoert nicht zur aktuell offenen Fahrt. AktuelleFahrtID={AktuelleFahrtID}, ErwartetAuftrag={ErwartetAuftrag}, ErwartetTeilfahrt={ErwartetTeilfahrt}, EmpfangenerAuftrag={IstAuftrag}, EmpfangeneTeilfahrt={IstTeilfahrt}.",
               aktuelleFahrt.AktuelleFahrtID,
               aktuelleFahrt.AuftragID,
               erwarteteTeilfahrt,
               payload.AuftragId,
               payload.TeilfahrtID);
            return false;
         }

         return true;
      }

      private void QueueKranfahrtBeendetEvent(KranfahrtBeendetPayload payload)
      {
         var kranEvent = new KranfahrtBeendetEvent(
            payload.AuftragId,
            payload.TeilfahrtID,
            payload.KranQuelle,
            payload.KranZiel,
            payload.Status,
            payload.IstGewicht,
            payload.AenderungsZaehler);

         if (!_eventQueue.Writer.TryWrite(kranEvent))
         {
            _logger.LogError("0027|KranfahrtBeendetEvent konnte nicht in die Event-Queue geschrieben werden.");
            return;
         }
         _logger.LogInformation(
            "0028|KranfahrtBeendetEvent eingereiht: Auftrag={AuftragId}, Teilfahrt={TeilfahrtID}, Quelle={Quelle}, Ziel={Ziel}, Status={Status}, IstGewicht={IstGewicht}, AenderungsZaehler={AenderungsZaehler}",
            payload.AuftragId,
            payload.TeilfahrtID,
            payload.KranQuelle,
            payload.KranZiel,
            payload.Status,
            payload.IstGewicht,
            payload.AenderungsZaehler);

         SendKranfahrtBeendetLiveSnapshot(
            payload.AenderungsZaehler,
            payload.AuftragId,
            payload.TeilfahrtID,
            payload.KranQuelle,
            payload.KranZiel,
            payload.Status,
            payload.IstGewicht);
      }

      private sealed record KranfahrtBeendetPayload(
         int AuftragId,
         int TeilfahrtID,
         string KranQuelle,
         string KranZiel,
         int Status,
         double IstGewicht,
         int AenderungsZaehler);

      private sealed record LkwPlatzLeerPayload(
         int AuftragID,
         int TeilfahrtID,
         int LkwPlatzPositionID,
         int AenderungsZaehler);
      #region Verbindungs-Event-Handler

      private void OnClientStateChanged(object? sender, OpcClientStateChangedEventArgs e)
      {
         _logger.LogDebug("002A|Kran-OPC Client Zustand geaendert von {OldState} zu {NewState}", e.OldState, e.NewState);

         if (e.NewState == OpcClientState.Connected)
         {
            MarkOpcDataFlowChecking("Verbunden, Datenfluss wird geprueft", "Event_201 wird geprueft");
            _logger.LogInformation("002B|OPC UA Client verbunden / wiederverbunden. Warte auf Event_201-Datenfluss.");
         }
         else if (e.NewState == OpcClientState.Disconnected)
         {
            MarkOpcDataFlowUnavailable("Getrennt", "OPC getrennt");
            _logger.LogError("002C|Die Verbindung zum OPC UA Server wurde getrennt!");
            StartBackgroundReconnectLoop("OPC Client meldet Disconnected");
         }
         else if (e.NewState == OpcClientState.Reconnecting)
         {
            MarkOpcDataFlowUnavailable("Reconnect laeuft", "OPC Reconnect laeuft");
            _logger.LogInformation("002D|Verbindung verloren. Auto-Reconnect versucht gerade die Wiederverbindung...");
            StartBackgroundReconnectLoop("OPC Client meldet Reconnecting");
         }
      }

      private void OnCwClientStateChanged(object? sender, OpcClientStateChangedEventArgs e)
      {
         _logger.LogDebug("0327|CW-OPC Client Zustand geaendert von {OldState} zu {NewState}", e.OldState, e.NewState);

         if (e.NewState == OpcClientState.Connected)
         {
            _runtimeStatus.SetOpcCwSpsStatus(true, "Verbunden");
            _logger.LogInformation("0328|CW-OPC Client erfolgreich verbunden / wiederverbunden.");
         }
         else if (e.NewState == OpcClientState.Disconnected)
         {
            _runtimeStatus.SetCwLebensZaehlerUnavailable("CW OPC getrennt");
            _logger.LogError("0329|Die Verbindung zur CW-SPS wurde getrennt.");
            StartCwBackgroundReconnectLoop("CW-OPC Client meldet Disconnected");
         }
         else if (e.NewState == OpcClientState.Reconnecting)
         {
            _runtimeStatus.SetCwLebensZaehlerUnavailable("CW Reconnect laeuft");
            _logger.LogInformation("032A|CW-Verbindung verloren. Auto-Reconnect versucht gerade die Wiederverbindung...");
            StartCwBackgroundReconnectLoop("CW-OPC Client meldet Reconnecting");
         }
      }

      private void OnEOfenClientStateChanged(object? sender, OpcClientStateChangedEventArgs e)
      {
         _logger.LogDebug("0527|E-Ofen-OPC Client Zustand geaendert von {OldState} zu {NewState}", e.OldState, e.NewState);

         if (e.NewState == OpcClientState.Connected)
         {
            _runtimeStatus.SetOpcEOfenSpsStatus(true, "Verbunden");
            _logger.LogInformation("0528|E-Ofen-OPC Client erfolgreich verbunden / wiederverbunden.");
         }
         else if (e.NewState == OpcClientState.Disconnected)
         {
            _runtimeStatus.SetEOfenLebensZaehlerUnavailable("E-Ofen OPC getrennt");
            _logger.LogError("0529|Die Verbindung zur E-Ofen-SPS wurde getrennt.");
            StartEOfenBackgroundReconnectLoop("E-Ofen-OPC Client meldet Disconnected");
         }
         else if (e.NewState == OpcClientState.Reconnecting)
         {
            _runtimeStatus.SetEOfenLebensZaehlerUnavailable("E-Ofen Reconnect laeuft");
            _logger.LogInformation("052A|E-Ofen-Verbindung verloren. Auto-Reconnect versucht gerade die Wiederverbindung...");
            StartEOfenBackgroundReconnectLoop("E-Ofen-OPC Client meldet Reconnecting");
         }
      }

      #endregion

      static void TraegerLicense()
      {
         Opc.UaFx.Client.Licenser.LicenseKey = ConfigManager.TraegerLicenseKey;
         var license = Opc.UaFx.Client.Licenser.LicenseInfo;
         Console.WriteLine("Traeger Licence Info = {0} | Gueltig = {1}", license.ToString(), !license.IsEvaluation);
      }
   }
}
















