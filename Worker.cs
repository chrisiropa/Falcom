using Opc.UaFx.Client;

namespace Falcom
{
   public class Worker : BackgroundService
   {
      private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

      private readonly ILogger<Worker> _logger;
      private readonly ConfigManager _configManager;
      private readonly OPC_Client_Crane _opcClientCrane;
      private ProcessState _currentState;

      public Worker(ILogger<Worker> logger, ConfigManager configManager, OPC_Client_Crane opcClientCrane)
      {
         _logger = logger;
         _configManager = configManager;
         _opcClientCrane = opcClientCrane;
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
         _logger.LogInformation("State machine started.");

         try
         {
            await _opcClientCrane.ConnectUntilConnectedAsync(stoppingToken);

            // Start state
            while (!stoppingToken.IsCancellationRequested)
            {
               await _opcClientCrane.EnsureDataFlowAsync(stoppingToken);

               if (_logger.IsEnabled(LogLevel.Information))
               {
                  _logger.LogInformation("Current state: {state}", _currentState);
               }

               switch (_currentState)
               {
                  case ProcessState.Idle:
                     break;
                  case ProcessState.ScrapStored:
                     _logger.LogInformation("Placeholder state: ScrapStored.");
                     // TODO: Implement logic
                     _currentState = ProcessState.Idle;
                     break;

                  case ProcessState.FineDosingComplete:
                     _logger.LogInformation("Placeholder state: FineDosingComplete.");
                     // TODO: Implement logic
                     _currentState = ProcessState.Idle;
                     break;

                  case ProcessState.MeltingProcessStarted:
                     _logger.LogInformation("Placeholder state: MeltingProcessStarted.");
                     // TODO: Implement logic
                     _currentState = ProcessState.Idle;
                     break;

                  default:
                     _logger.LogError("Unhandled state: {state}. Resetting to Idle.", _currentState);
                     _currentState = ProcessState.Idle;
                     break;
               }

               LogOpenCraneQueueOrders();

               await Task.Delay(PollInterval, stoppingToken);
            }
         }
         catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
         {
            _logger.LogInformation("State machine stopping.");
         }
         finally
         {
            _opcClientCrane.Disconnect();
         }
      }

      public override async Task StopAsync(CancellationToken cancellationToken)
      {
         _logger.LogInformation("Windows-Dienst Stop angefordert.");
         await base.StopAsync(cancellationToken);
      }

      private void LogOpenCraneQueueOrders()
      {
         const string sql = @"
            SELECT ID,
                   Reihenfolge,
                   Prioritaet,
                   AuftragsTyp,
                   AuftragID,
                   ErstelltDatumZeit,
                   Bemerkung
            FROM dbo.FALCOM_KRAN_QUEUE
            WHERE FertigDatumZeit IS NULL
            ORDER BY Prioritaet ASC,
                     Reihenfolge ASC,
                     ErstelltDatumZeit ASC,
                     ID ASC";

         SimpleSqlQuery query = new(_configManager.ConnectionString, sql);

         if (query.Exception is not null)
         {
            _logger.LogError(query.Exception, "Tabelle FALCOM_KRAN_QUEUE konnte nicht abgefragt werden.");
            return;
         }

         if (query.QueryResult is null || query.QueryResult.Count == 0)
         {
            return;
         }

         _logger.LogInformation("{count} offene Kran-Auftraege in FALCOM_KRAN_QUEUE gefunden.", query.QueryResult.Count);

         foreach (Dictionary<string, object> row in query.QueryResult)
         {
            _logger.LogInformation(
               "Offener Kran-Auftrag: QueueID={queueId}, Reihenfolge={reihenfolge}, Prioritaet={prioritaet}, AuftragsTyp={auftragsTyp}, AuftragID={auftragId}, Erstellt={erstellt}, Bemerkung={bemerkung}",
               row["ID"],
               row["Reihenfolge"],
               row["Prioritaet"],
               row["AuftragsTyp"],
               row["AuftragID"],
               row["ErstelltDatumZeit"],
               row["Bemerkung"]);
         }
      }
   }
}
