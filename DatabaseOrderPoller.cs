using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Falcom
{
   /// <summary>
   /// Dieser Hintergrunddienst überwacht zyklisch die SQL-Datenbank auf neue, freigegebene
   /// Chargieraufträge und schleust diese bei freier Bahn in die interne Event-Queue ein.
   /// </summary>
   public sealed class DatabaseOrderPoller : BackgroundService
   {
      private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

      private readonly ILogger<DatabaseOrderPoller> _logger;
      private readonly ConfigManager _configManager;
      private readonly FalcomEventQueue _eventQueue;

      public DatabaseOrderPoller(
          ILogger<DatabaseOrderPoller> logger,
          ConfigManager configManager,
          FalcomEventQueue eventQueue)
      {
         _logger = logger;
         _configManager = configManager;
         _eventQueue = eventQueue;
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
         _logger.LogInformation("Datenbank-Poller gestartet. Nutze Stored Procedures. Intervall: {Interval}s", PollInterval.TotalSeconds);

         while (!stoppingToken.IsCancellationRequested)
         {
            try
            {
               _logger.LogInformation("Datenbank-Poller works...");



               // 1. Prüfen, ob aktuell bereits ein Auftrag aktiv bearbeitet wird
               if (IsAnyOrderInProgress())
               {
                  _logger.LogInformation("Auftrag läuft noch...");

                  // Ein Auftrag läuft -> Poller schläft sofort wieder, um Doppelungen zu vermeiden
                  await Task.Delay(PollInterval, stoppingToken);
                  continue;
               }

               // 2. Wenn die Luft rein ist: Den ältesten freigegebenen Auftrag via SP ermitteln
               var nextOrder = FetchNextReleasedOrder();

               if (nextOrder is not null)
               {
                  _logger.LogInformation("Neuen freigegebenen Auftrag gefunden: ID={OrderId}", nextOrder.AuftragsNummer);

                  // 3. Status in der DB sofort auf 'IN_ARBEIT' (Zustand 2) setzen,
                  // damit dieser Poller beim nächsten Durchlauf sperrt.
                  if (UpdateOrderStatus(nextOrder.AuftragsNummer, 2))
                  {
                     // 4. Das Event in den C#-Kanal (Queue) pushen, damit der Dispatcher-Worker erwacht
                     _logger.LogInformation("Feuere OrderReleasedEvent fuer Auftrag {OrderId} ab.", nextOrder.AuftragsNummer);
                     await _eventQueue.PushEventAsync(nextOrder);
                  }
               }
            }
            catch (OperationCanceledException)
            {
               // Dienst wird regulär beendet
               throw;
            }
            catch (Exception ex)
            {
               // Wir loggen ex.Message statt der ganzen ex-Instanz. 
               // Das schneidet den Callstack komplett ab!
               _logger.LogError("Fehler beim Pollen der Auftragsdatenbank über Stored Procedures. Meldung: {Message}", ex.Message);
            }

            // Reguläre Pause vor dem nächsten Datenbank-Check
            await Task.Delay(PollInterval, stoppingToken);
         }
      }

      /// <summary>
      /// Ruft die SP 'FALCOM_IsOrderInProgress' auf, um zu prüfen, ob ein Auftrag mit Status 2 existiert.
      /// </summary>
      private bool IsAnyOrderInProgress()
      {
         SimpleSqlQuery query = new(_configManager.ConnectionString, "EXEC dbo.FALCOM_IsOrderInProgress");

         if (query.Exception is not null)
         {
            _logger.LogError(query.Exception, "Fehler bei FALCOM_IsOrderInProgress.");
            return true; // Im Fehlerfall blockieren wir sicherheitshalber den Start weiterer Aufträge
         }

         if (query.QueryResult is not null && query.QueryResult.Count > 0)
         {
            var isActive = Convert.ToInt32(query.QueryResult[0]["IsActive"]);
            return isActive == 1;
         }

         return false;
      }

      /// <summary>
      /// Ruft die SP 'FALCOM_GetNextReleasedOrder' auf und wandelt das Ergebnis in ein C#-Event um.
      /// </summary>
      private OrderReleasedEvent? FetchNextReleasedOrder()
      {
         SimpleSqlQuery query = new(_configManager.ConnectionString, "EXEC dbo.FALCOM_GetNextReleasedOrder");

         if (query.Exception is not null)
         {
            _logger.LogError(query.Exception.Message, "Fehler bei FALCOM_GetNextReleasedOrder.");
            return null;
         }

         if (query.QueryResult is not null && query.QueryResult.Count > 0)
         {
            var row = query.QueryResult[0];

            // Instanziierung des Events mit den echten angepassten Spaltennamen aus eurer DB
            return new OrderReleasedEvent(
               auftragsNummer: Convert.ToInt32(row["ID"]),
               eisensorteId: row["EisensorteID"]?.ToString() ?? "UNBEKANNT",
               zielGewichtKg: Convert.ToDouble(row["ZielgewichtKg"])
            );
         }

         return null;
      }

      /// <summary>
      /// Ruft die SP 'FALCOM_UpdateOrderStatus' auf, um den Zustand eines Auftrags fortzuschreiben.
      /// </summary>
      private bool UpdateOrderStatus(int orderId, int newStatus)
      {
         string sql = $"EXEC dbo.FALCOM_UpdateOrderStatus @OrderId = {orderId}, @NewStatus = {newStatus}";

         SimpleSqlQuery query = new(_configManager.ConnectionString, sql);

         if (query.Exception is not null)
         {
            _logger.LogError(query.Exception, "Fehler bei FALCOM_UpdateOrderStatus fuer ID {OrderId}.", orderId);
            return false;
         }

         if (query.QueryResult is not null && query.QueryResult.Count > 0)
         {
            var rowsAffected = Convert.ToInt32(query.QueryResult[0]["RowsAffected"]);
            return rowsAffected > 0;
         }

         return true;
      }
   }
}