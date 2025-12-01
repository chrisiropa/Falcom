namespace Falcom
{
   public class Worker : BackgroundService
   {
      private readonly ILogger<Worker> _logger;
      private readonly Logging logging;
      private readonly Parameter parameter;
      private readonly Lager lager;

      public Worker(ILogger<Worker> logger, ConfigManager configManager)
      {
         _logger = logger;
         logging = new Logging(configManager);
         parameter = new Parameter(configManager, logging);
         lager = new Lager(configManager, logging);
      }

      public Parameter Parameter
      {
         get { return parameter; }
      }

      public Lager Lager
      {
         get
         {
            return lager;
         }
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
         logging.Run();

         while (!stoppingToken.IsCancellationRequested)
         {
            if (_logger.IsEnabled(LogLevel.Information))
            {
               _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            Optimizer opti = new Optimizer(lager, parameter, logging);

            opti.Run();


            await Task.Delay(1000, stoppingToken);
         }


         logging.Stop();
      }
   }
}
