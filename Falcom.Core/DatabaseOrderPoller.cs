using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Falcom
{
   /// <summary>
   /// Ueberwacht zyklisch die SQL-Datenbank auf die naechste fachlich anstehende Kranfahrt.
   /// Die fachliche Prioritaet liegt in der Datenbank: zuerst Chargieren, danach Einlagern.
   /// </summary>
   public sealed class DatabaseOrderPoller : BackgroundService
   {
      private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);
      private static readonly TimeSpan PollSummaryInterval = TimeSpan.FromMinutes(1);

      private readonly ILogger<DatabaseOrderPoller> _logger;
      private readonly ConfigManager _configManager;
      private readonly FalcomEventQueue _eventQueue;
      private readonly FalcomRuntimeStatus _runtimeStatus;
      private int pollCount;
      private int blockedPollCount;
      private int idlePollCount;
      private DateTime nextPollSummaryUtc = DateTime.UtcNow.Add(PollSummaryInterval);

      public DatabaseOrderPoller(
         ILogger<DatabaseOrderPoller> logger,
         ConfigManager configManager,
         FalcomEventQueue eventQueue,
         FalcomRuntimeStatus runtimeStatus)
      {
         _logger = logger;
         _configManager = configManager;
         _eventQueue = eventQueue;
         _runtimeStatus = runtimeStatus;
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
         _logger.LogInformation(
            "0004|Datenbank-Poller gestartet. Suche naechste Kranfahrt ueber Stored Procedures. Intervall: {Interval}s",
            PollInterval.TotalSeconds);

         while (!stoppingToken.IsCancellationRequested)
         {
            try
            {
               pollCount++;
               _runtimeStatus.SetAuftragsPollerPruefung();
               _runtimeStatus.SetFreigabePruefung();

               if (IsAnyOrderInProgress())
               {
                  blockedPollCount++;
                  LogPollSummaryIfDue();
                  await Task.Delay(PollInterval, stoppingToken);
                  continue;
               }

               PendingKranfahrt pendingKranfahrt = FetchPendingKranfahrt();

               if (pendingKranfahrt.HasPending)
               {
                  _logger.LogInformation(
                     "0007|Naechste Kranfahrt steht an: Typ={AuftragsTyp}, AuftragID={AuftragID}, Grund={Reason}",
                     pendingKranfahrt.AuftragsTyp,
                     pendingKranfahrt.AuftragID,
                     pendingKranfahrt.Reason);

                  await _eventQueue.PushEventAsync(
                     new NextKranfahrtAvailableEvent(
                        pendingKranfahrt.AuftragsTyp,
                        pendingKranfahrt.AuftragID,
                        pendingKranfahrt.Reason));
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
                  "0009|Fehler beim Pollen der naechsten Kranfahrt ueber Stored Procedures. Meldung: {Message}",
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
            "0005|Datenbank-Poller aktiv. In den letzten {Seconds:N0} Sekunden: Polls={PollCount}, blockiert={BlockedPollCount}, ohne neue Kranfahrt={IdlePollCount}.",
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

      private PendingKranfahrt FetchPendingKranfahrt()
      {
         try
         {
            using SqlConnection connection = new(_configManager.ConnectionString);
            using SqlCommand command = CreateStoredProcedureCommand(
               connection,
               "dbo.FALCOM_HasPendingKranfahrt");

            _runtimeStatus.SetFreigabePruefung();
            connection.Open();
            using SqlDataReader reader = command.ExecuteReader();

            if (!reader.Read())
            {
               return PendingKranfahrt.None("FALCOM_HasPendingKranfahrt lieferte kein Ergebnis.");
            }

            bool hasPending = Convert.ToBoolean(reader["HasPending"]);
            return new PendingKranfahrt(
               hasPending,
               reader["AuftragsTyp"]?.ToString() ?? string.Empty,
               reader["AuftragID"] == DBNull.Value ? null : Convert.ToInt64(reader["AuftragID"]),
               reader["Reason"]?.ToString() ?? string.Empty);
         }
         catch (Exception ex)
         {
            _logger.LogError(ex, "000B|Fehler bei FALCOM_HasPendingKranfahrt.");
            return PendingKranfahrt.None(ex.Message);
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

      private sealed record PendingKranfahrt(
         bool HasPending,
         string AuftragsTyp,
         long? AuftragID,
         string Reason)
      {
         public static PendingKranfahrt None(string reason)
         {
            return new PendingKranfahrt(false, string.Empty, null, reason);
         }
      }
   }
}
