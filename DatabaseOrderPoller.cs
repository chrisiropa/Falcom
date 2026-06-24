using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Falcom
{
   /// <summary>
   /// Überwacht zyklisch die SQL-Datenbank auf neue, freigegebene Chargieraufträge
   /// und schleust diese bei freier Bahn in die interne Event-Queue ein.
   /// </summary>
   public sealed class DatabaseOrderPoller : BackgroundService
   {
      private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
      private static readonly TimeSpan PollSummaryInterval = TimeSpan.FromMinutes(1);

      private readonly ILogger<DatabaseOrderPoller> _logger;
      private readonly ConfigManager _configManager;
      private readonly FalcomEventQueue _eventQueue;
      private int pollCount;
      private int blockedPollCount;
      private int idlePollCount;
      private DateTime nextPollSummaryUtc = DateTime.UtcNow.Add(PollSummaryInterval);

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
         _logger.LogInformation(
            "0004|Datenbank-Poller gestartet. Nutze Stored Procedures. Intervall: {Interval}s",
            PollInterval.TotalSeconds);

         while (!stoppingToken.IsCancellationRequested)
         {
            try
            {
               pollCount++;

               if (IsAnyOrderInProgress())
               {
                  blockedPollCount++;
                  LogPollSummaryIfDue();
                  await Task.Delay(PollInterval, stoppingToken);
                  continue;
               }

               OrderReleasedEvent? nextOrder = FetchNextReleasedOrder();

               if (nextOrder is not null)
               {
                  _logger.LogInformation(
                     "0007|Neuen freigegebenen Auftrag gefunden: ID={OrderId}",
                     nextOrder.AuftragsNummer);

                  if (UpdateOrderStatus(nextOrder.AuftragsNummer, 2))
                  {
                     _logger.LogInformation(
                        "0008|Feuere OrderReleasedEvent fuer Auftrag {OrderId} ab.",
                        nextOrder.AuftragsNummer);
                     await _eventQueue.PushEventAsync(nextOrder);
                  }
               }
               else
               {
                  idlePollCount++;
               }

               LogPollSummaryIfDue();
            }
            catch (OperationCanceledException)
            {
               throw;
            }
            catch (Exception ex)
            {
               _logger.LogError(
                  "0009|Fehler beim Pollen der Auftragsdatenbank über Stored Procedures. Meldung: {Message}",
                  ex.Message);
            }

            await Task.Delay(PollInterval, stoppingToken);
         }
      }

      private void LogPollSummaryIfDue()
      {
         DateTime nowUtc = DateTime.UtcNow;

         if (nowUtc < nextPollSummaryUtc)
         {
            return;
         }

         _logger.LogInformation(
            "0005|Datenbank-Poller aktiv. In den letzten {Seconds:N0} Sekunden: Polls={PollCount}, blockiert={BlockedPollCount}, ohne neuen Auftrag={IdlePollCount}.",
            PollSummaryInterval.TotalSeconds,
            pollCount,
            blockedPollCount,
            idlePollCount);

         pollCount = 0;
         blockedPollCount = 0;
         idlePollCount = 0;
         nextPollSummaryUtc = nowUtc.Add(PollSummaryInterval);
      }

      private bool IsAnyOrderInProgress()
      {
         try
         {
            using SqlConnection connection = new(_configManager.ConnectionString);
            using SqlCommand command = CreateStoredProcedureCommand(
               connection,
               "dbo.FALCOM_IsOrderInProgress");

            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();

            if (!reader.Read())
            {
               return false;
            }

            int isActive = Convert.ToInt32(reader["IsActive"]);
            return isActive == 1;
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, "000A|Fehler bei FALCOM_IsOrderInProgress.");
            return true;
         }
      }

      private OrderReleasedEvent? FetchNextReleasedOrder()
      {
         try
         {
            using SqlConnection connection = new(_configManager.ConnectionString);
            using SqlCommand command = CreateStoredProcedureCommand(
               connection,
               "dbo.FALCOM_GetNextReleasedOrder");

            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();

            if (!reader.Read())
            {
               return null;
            }

            return new OrderReleasedEvent(
               auftragsNummer: Convert.ToInt32(reader["ID"]),
               eisensorteId: reader["EisensorteID"]?.ToString() ?? "UNBEKANNT",
               zielGewichtKg: Convert.ToDouble(reader["ZielgewichtKg"]));
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, "000B|Fehler bei FALCOM_GetNextReleasedOrder.");
            return null;
         }
      }

      private bool UpdateOrderStatus(int orderId, int newStatus)
      {
         try
         {
            using SqlConnection connection = new(_configManager.ConnectionString);
            using SqlCommand command = CreateStoredProcedureCommand(
               connection,
               "dbo.FALCOM_UpdateOrderStatus");

            command.Parameters.Add("@OrderId", SqlDbType.BigInt).Value = orderId;
            command.Parameters.Add("@NewStatus", SqlDbType.Int).Value = newStatus;

            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();

            if (!reader.Read())
            {
               return true;
            }

            int rowsAffected = Convert.ToInt32(reader["RowsAffected"]);
            return rowsAffected > 0;
         }
         catch (Exception ex)
         {
            _logger.LogError(
               ex,
               "000C|Fehler bei FALCOM_UpdateOrderStatus fuer ID {OrderId}.",
               orderId);
            return false;
         }
      }

      private static SqlCommand CreateStoredProcedureCommand(
         SqlConnection connection,
         string procedureName)
      {
         return new SqlCommand(procedureName, connection)
         {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 30
         };
      }
   }
}
