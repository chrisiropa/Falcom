using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Opc.UaFx.Client;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Falcom
{
   public class Worker : BackgroundService
   {
      private readonly ILogger<Worker> _logger;
      private readonly ConfigManager _configManager;
      private readonly OPC_Client_Crane _opcClientCrane;
      private readonly FalcomEventQueue _eventQueue; // NEU: Die Event-Queue injizieren
      private readonly WatchdogSender _watchdogSender;
      private readonly AktuelleFahrtRepository _aktuelleFahrtRepository;
      private readonly FalcomRuntimeStatus _runtimeStatus;
      private ProcessState _currentState;
      private ProcessState? _lastLoggedState;
      private int watchdogEventPending;
      private int watchdogValue;
      private int watchdogEventsInCurrentMinute;
      private DateTime nextWatchdogSummaryUtc = DateTime.UtcNow.AddMinutes(1);

      public Worker(
          ILogger<Worker> logger,
          ConfigManager configManager,
          OPC_Client_Crane opcClientCrane,
          FalcomEventQueue eventQueue,
          WatchdogSender watchdogSender,
          AktuelleFahrtRepository aktuelleFahrtRepository,
          FalcomRuntimeStatus runtimeStatus) // NEU: Im Konstruktor übergeben
      {
         _logger = logger;
         _configManager = configManager;
         _opcClientCrane = opcClientCrane;
         _eventQueue = eventQueue; // NEU
         _watchdogSender = watchdogSender;
         _aktuelleFahrtRepository = aktuelleFahrtRepository;
         _runtimeStatus = runtimeStatus;
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
         try
         {
            _logger.LogInformation("002E|State machine started.");
            _logger.LogInformation("004C|Initialer Verbindungsaufbau zur Kran-SPS wird gestartet.");
            await _opcClientCrane.ConnectUntilConnectedAsync(stoppingToken);
            _logger.LogInformation("004D|Initialer Verbindungsaufbau zur Kran-SPS ist abgeschlossen.");

            using var watchdogTimerCancellation =
               CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            Task watchdogTimer = ScheduleWatchdogEventsAsync(
               watchdogTimerCancellation.Token);
            Task opcDataFlowMonitor = ScheduleOpcDataFlowChecksAsync(
               watchdogTimerCancellation.Token);

            // NEU: Die Schleife wartet jetzt reaktiv, bis ein Event in der Queue landet.
            // WaitToReadAsync lässt den Thread schlafen, solange die Queue leer ist (0% CPU Last).
            try
            {
               InitializeStateFromDatabase();

               while (await _eventQueue.Reader.WaitToReadAsync(stoppingToken))
               {
                  // Verarbeite alle aktuell in der Queue liegenden Events der Reihe nach (FIFO)
                  while (_eventQueue.Reader.TryRead(out var falcomEvent))
                  {
                     try
                     {
                        _logger.LogDebug("0030|Dispatcher verarbeitet Event {EventId} von Quelle {Source}.", falcomEvent.EventId, falcomEvent.Source);

                        if (falcomEvent is WatchdogEvent watchdogEvent)
                        {
                           try
                           {
                              watchdogEventsInCurrentMinute++;
                              LogWatchdogSummaryIfDue(watchdogEvent.LebensZaehler);

                              await _watchdogSender.SendAsync(
                                 watchdogEvent.LebensZaehler,
                                 stoppingToken);
                           }
                           finally
                           {
                              Interlocked.Exchange(
                                 ref watchdogEventPending,
                                 0);
                           }

                           continue;
                        }

                        if (falcomEvent is KranSpsLebensZaehlerEvent)
                        {
                           continue;
                        }

                        if (falcomEvent is OrderReleasedEvent orderReleasedEvent)
                        {
                           SetState(ProcessState.AuftragBereit);

                           AktuelleFahrtResult result =
                              _aktuelleFahrtRepository.TryCreateNextAktuelleFahrt(
                                 orderReleasedEvent.AuftragsNummer);

                           _logger.LogInformation(
                              "0045|Aktuelle Fahrt aus Auftrag erzeugt: Erfolg={Success}, Grund={Reason}, AktuelleFahrtID={AktuelleFahrtID}, AuftragID={AuftragID}, Typ={AuftragsTyp}, Quelle={Quelle}, Ziel={Ziel}, SollMengeKg={SollMengeKg}.",
                              result.Success,
                              result.Reason,
                              result.AktuelleFahrtID,
                              result.AuftragID,
                              result.AuftragsTyp,
                              result.Quelle,
                              result.Ziel,
                              result.SollMengeKg);

                           if (result.Success)
                           {
                              _runtimeStatus.SetAktuelleFahrt(result);

                              KranfahrtAuftragEvent kranfahrtAuftragEvent =
                                 KranfahrtAuftragEvent.FromAktuelleFahrt(
                                    result,
                                    auftragTeilfahrt: 1,
                                    toleranzKg: 150m);

                              OPC_Client_Crane.OpcSendResult sendResult =
                                 await _opcClientCrane.SendKranfahrtAuftragAsync(
                                    kranfahrtAuftragEvent,
                                    stoppingToken);

                              if (!sendResult.Success)
                              {
                                 string bemerkung =
                                    $"FEHLER beim Starten der Kranfahrt: {sendResult.Reason}";

                                 AktuelleFahrtResult failResult =
                                    _aktuelleFahrtRepository.FailAktuelleFahrt(
                                       result.AktuelleFahrtID,
                                       bemerkung);

                                 _runtimeStatus.SetAktuelleFahrt(AktuelleFahrtResult.Empty(
                                    "Keine aktive Fahrt."));

                                 _logger.LogError(
                                    "0052|Aktuelle Fahrt wegen nicht sendbarem SPS-Fahrauftrag historisiert: Erfolg={Success}, Grund={Reason}, AktuelleFahrtID={AktuelleFahrtID}, AuftragID={AuftragID}, Bemerkung={Bemerkung}.",
                                    failResult.Success,
                                    failResult.Reason,
                                    failResult.AktuelleFahrtID,
                                    failResult.AuftragID,
                                    bemerkung);

                                 SetState(ProcessState.Fehler);
                              }
                              else
                              {
                                 SetState(ProcessState.FahrtAnSpsGesendet);
                                 SetState(ProcessState.WarteAufSpsRueckmeldung);
                              }
                           }
                           else
                           {
                              SetState(ProcessState.Fehler);
                           }
                        }

                        if (falcomEvent is KranfahrtBeendetEvent kranfahrtBeendetEvent)
                        {
                           AktuelleFahrtResult result =
                              _aktuelleFahrtRepository.CompleteAktuelleFahrt(
                                 kranfahrtBeendetEvent);

                           _logger.LogInformation(
                              "0046|KranfahrtBeendet verarbeitet: Erfolg={Success}, Grund={Reason}, AktuelleFahrtID={AktuelleFahrtID}, AuftragID={AuftragID}, Typ={AuftragsTyp}, IstMengeKg={IstMengeKg}.",
                              result.Success,
                              result.Reason,
                              result.AktuelleFahrtID,
                              result.AuftragID,
                              result.AuftragsTyp,
                              result.IstMengeKg);

                           if (result.Success)
                           {
                              SetState(ProcessState.FahrtAbgeschlossen);
                              SetState(ProcessState.Idle);
                           }
                           else
                           {
                              SetState(ProcessState.Fehler);
                           }
                        }

                        // 1. Datenfluss zur SPS sicherstellen
                        await _opcClientCrane.EnsureDataFlowAsync(stoppingToken);

                        // Optionale Überwachungsausgabe
                        //LogOpenCraneQueueOrdersIfChanged();
                     }
                     catch (OperationCanceledException)
                     {
                        throw;
                     }
                     catch (Exception ex)
                     {
                        _logger.LogError(ex, "0035|Fehler im State-Machine-Zyklus (z.B. OPC-Verbindungsverlust). Zustand war: {state}", _currentState);

                        SetState(ProcessState.Fehler);

                        // Dem System im Fehlerfall etwas Zeit zum Atmen geben
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                        // Da ein Fehler auftrat, brechen wir die innere TryRead-Schleife ab,
                        // um den Datenfluss im nächsten Hauptdurchlauf frisch zu prüfen.
                        break;
                     }
                  }
               }
            }
            finally
            {
               watchdogTimerCancellation.Cancel();

               try
               {
                  await watchdogTimer;
               }
               catch (OperationCanceledException)
               {
               }

               try
               {
                  await opcDataFlowMonitor;
               }
               catch (OperationCanceledException)
               {
               }

               _logger.LogInformation("0038|Disconnecting from OPC Server...");
               _opcClientCrane.Disconnect();
            }
         }
         catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
         {
            _logger.LogInformation("0036|State machine stopping via CancellationToken.");
         }
         catch (Exception ex)
         {
            _logger.LogCritical(ex, "0037|Schwerwiegender Fehler außerhalb der Hauptschleife. Dienst wird beendet!");
         }
      }

      private void LogWatchdogSummaryIfDue(int currentLebensZaehler)
      {
         DateTime nowUtc = DateTime.UtcNow;

         if (nowUtc < nextWatchdogSummaryUtc)
         {
            return;
         }

         _logger.LogInformation(
            "0043|Watchdog aktiv. In den letzten 60 Sekunden wurden {WatchdogCount} Watchdog-Events verarbeitet. Aktueller LebensZaehler={LebensZaehler}.",
            watchdogEventsInCurrentMinute,
            currentLebensZaehler);

         watchdogEventsInCurrentMinute = 0;
         nextWatchdogSummaryUtc = nowUtc.AddMinutes(1);
      }

      private void InitializeStateFromDatabase()
      {
         AktuelleFahrtResult aktuelleFahrt =
            _aktuelleFahrtRepository.GetAktuelleFahrt();

         if (aktuelleFahrt.Success && aktuelleFahrt.AktuelleFahrtID is not null)
         {
            _runtimeStatus.SetAktuelleFahrt(aktuelleFahrt);

            _logger.LogInformation(
               "0054|Programmstart: Aktuelle Fahrt aus Datenbank erkannt. Rekonstruiere Zustand WarteAufSpsRueckmeldung. AktuelleFahrtID={AktuelleFahrtID}, AuftragID={AuftragID}, Typ={AuftragsTyp}, QuellePositionID={QuellePositionID}, ZielPositionID={ZielPositionID}, Quelle={Quelle}, Ziel={Ziel}, SollMengeKg={SollMengeKg}.",
               aktuelleFahrt.AktuelleFahrtID,
               aktuelleFahrt.AuftragID,
               aktuelleFahrt.AuftragsTyp,
               aktuelleFahrt.QuellePositionID,
               aktuelleFahrt.ZielPositionID,
               aktuelleFahrt.Quelle,
               aktuelleFahrt.Ziel,
               aktuelleFahrt.SollMengeKg);

            SetState(ProcessState.WarteAufSpsRueckmeldung);
            return;
         }

         _logger.LogInformation(
            "0055|Programmstart: Keine aktuelle Fahrt in FALCOM_AKTUELLE_FAHRT gefunden. Zustand Idle.");
         SetState(ProcessState.Idle);
      }

      private void SetState(ProcessState nextState)
      {
         _currentState = nextState;

         if (_lastLoggedState == nextState)
         {
            return;
         }

         _logger.LogInformation(
            "002F|State changed: {previousState} -> {currentState}",
            _lastLoggedState?.ToString() ?? "INITIAL",
            nextState);

         _lastLoggedState = nextState;
      }

      private async Task ScheduleWatchdogEventsAsync(CancellationToken stoppingToken)
      {
         using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

         while (await timer.WaitForNextTickAsync(stoppingToken))
         {
            if (Interlocked.CompareExchange(
               ref watchdogEventPending,
               1,
               0) != 0)
            {
               continue;
            }

            await _eventQueue.Writer.WriteAsync(
               new WatchdogEvent(watchdogValue),
               stoppingToken);

            watchdogValue = watchdogValue == int.MaxValue
               ? 0
               : watchdogValue + 1;
         }
      }

      private async Task ScheduleOpcDataFlowChecksAsync(CancellationToken stoppingToken)
      {
         using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

         while (await timer.WaitForNextTickAsync(stoppingToken))
         {
            try
            {
               await _opcClientCrane.EnsureDataFlowAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
               throw;
            }
            catch (Exception ex)
            {
               _logger.LogWarning(
                  "0060|Zyklische OPC-Datenflusspruefung fehlgeschlagen. Fehler={ExceptionType}: {Message}",
                  ex.GetType().Name,
                  ex.Message);
            }
         }
      }

      public override async Task StopAsync(CancellationToken cancellationToken)
      {
         _logger.LogInformation("0039|Windows-Dienst Stop angefordert.");
         await base.StopAsync(cancellationToken);
      }

      
   }
}
