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
      private ProcessState _currentState;
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
          AktuelleFahrtRepository aktuelleFahrtRepository) // NEU: Im Konstruktor übergeben
      {
         _logger = logger;
         _configManager = configManager;
         _opcClientCrane = opcClientCrane;
         _eventQueue = eventQueue; // NEU
         _watchdogSender = watchdogSender;
         _aktuelleFahrtRepository = aktuelleFahrtRepository;
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
         _logger.LogInformation("002E|State machine started.");
         _logger.LogInformation("004C|Initialer Verbindungsaufbau zur Kran-SPS wird gestartet.");
         await _opcClientCrane.ConnectUntilConnectedAsync(stoppingToken);
         _logger.LogInformation("004D|Initialer Verbindungsaufbau zur Kran-SPS ist abgeschlossen.");

         using var watchdogTimerCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
         Task watchdogTimer = ScheduleWatchdogEventsAsync(
            watchdogTimerCancellation.Token);

         try
         {
            ProcessState? lastLoggedState = null;

            // NEU: Die Schleife wartet jetzt reaktiv, bis ein Event in der Queue landet.
            // WaitToReadAsync lässt den Thread schlafen, solange die Queue leer ist (0% CPU Last).
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
                           await _opcClientCrane.SendKranfahrtAuftragAsync(
                              result,
                              auftragTeilfahrt: 1,
                              toleranzKg: 150m,
                              aktiv: true,
                              stoppingToken);
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
                     }

                     // 1. Datenfluss zur SPS sicherstellen
                     await _opcClientCrane.EnsureDataFlowAsync(stoppingToken);

                     // 2. Zustandsänderung loggen
                     if (_currentState != lastLoggedState)
                     {
                        if (_logger.IsEnabled(LogLevel.Information))
                        {
                           _logger.LogInformation("002F|State changed: {previousState} -> {currentState}",
                              lastLoggedState?.ToString() ?? "INITIAL", _currentState);
                        }
                        lastLoggedState = _currentState;
                     }

                     // 3. NEU: Unterscheidung zwischen Haupt- (StateTrigger) und Neben-Events
                     if (falcomEvent.IsStateTrigger)
                     {
                        _logger.LogInformation("0031|[HAUPT-EVENT] Trigger fuer State Machine erkannt: {EventType}", falcomEvent.GetType().Name);

                        // State Machine Logik (Dein bestehender Switch-Block, jetzt ereignisgesteuert!)
                        switch (_currentState)
                        {
                           case ProcessState.Idle:
                              // TODO: Hier kannst du gezielt auf das konkrete Event reagieren, 
                              // z.B. wenn es ein OrderReleasedEvent ist:
                              // if (falcomEvent is OrderReleasedEvent orderEvent) { ... }
                              break;

                           case ProcessState.ConnectionLost:
                              _logger.LogInformation("0032|OPC-Verbindung stabilisiert. Kehre zurück zu Idle.");
                              _currentState = ProcessState.Idle;
                              break;

                           default:
                              _logger.LogError("0033|Unhandled state: {state}. Resetting to Idle.", _currentState);
                              _currentState = ProcessState.Idle;
                              break;
                        }
                     }
                     else
                     {
                        // Neben-Events: Gehen komplett an der State Machine vorbei
                        _logger.LogInformation("0034|[NEBEN-EVENT] Datentelemetrie direkt in DB/Cache schreiben: {EventType}", falcomEvent.GetType().Name);

                        // TODO: Direkt in DB-Historie wegschreiben, ohne den Kran-Ablauf zu stören
                     }

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

                     _currentState = ProcessState.ConnectionLost;
                     lastLoggedState = null;

                     // Dem System im Fehlerfall etwas Zeit zum Atmen geben
                     await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                     // Da ein Fehler auftrat, brechen wir die innere TryRead-Schleife ab,
                     // um den Datenfluss im nächsten Hauptdurchlauf frisch zu prüfen.
                     break;
                  }
               }
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

            _logger.LogInformation("0038|Disconnecting from OPC Server...");
            _opcClientCrane.Disconnect();
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

      public override async Task StopAsync(CancellationToken cancellationToken)
      {
         _logger.LogInformation("0039|Windows-Dienst Stop angefordert.");
         await base.StopAsync(cancellationToken);
      }

      
   }
}
