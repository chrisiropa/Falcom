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
      private ProcessState _currentState;

      public Worker(
          ILogger<Worker> logger,
          ConfigManager configManager,
          OPC_Client_Crane opcClientCrane,
          FalcomEventQueue eventQueue) // NEU: Im Konstruktor übergeben
      {
         _logger = logger;
         _configManager = configManager;
         _opcClientCrane = opcClientCrane;
         _eventQueue = eventQueue; // NEU
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
         _logger.LogInformation("002E|State machine started.");

         try
         {
            // Erstverbindung beim Start des Dienstes
            await _opcClientCrane.ConnectUntilConnectedAsync(stoppingToken);

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

                     _logger.LogDebug("0030|Dispatcher verarbeitet Event {EventId} von Quelle {Source}.", falcomEvent.EventId, falcomEvent.Source);

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
            _logger.LogInformation("0038|Disconnecting from OPC Server...");
            _opcClientCrane.Disconnect();
         }
      }

      public override async Task StopAsync(CancellationToken cancellationToken)
      {
         _logger.LogInformation("0039|Windows-Dienst Stop angefordert.");
         await base.StopAsync(cancellationToken);
      }

      
   }
}
