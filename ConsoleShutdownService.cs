namespace Falcom
{
   public sealed class ConsoleShutdownService : IHostedService
   {
      private readonly IHostApplicationLifetime _applicationLifetime;
      private readonly ILogger<ConsoleShutdownService> _logger;
      private int stopRequested;

      public ConsoleShutdownService(
         IHostApplicationLifetime applicationLifetime,
         ILogger<ConsoleShutdownService> logger)
      {
         _applicationLifetime = applicationLifetime;
         _logger = logger;
      }

      public Task StartAsync(CancellationToken cancellationToken)
      {
         if (!Environment.UserInteractive || Console.IsInputRedirected)
         {
            return Task.CompletedTask;
         }

         Thread thread = new(WaitForEnter)
         {
            IsBackground = true,
            Name = "Falcom console shutdown listener"
         };

         thread.Start();
         return Task.CompletedTask;
      }

      public Task StopAsync(CancellationToken cancellationToken)
      {
         return Task.CompletedTask;
      }

      private void WaitForEnter()
      {
         try
         {
            _logger.LogInformation("0001|Konsolenmodus aktiv. Enter druecken, um FALCOM geordnet zu beenden.");
            Console.ReadLine();

            if (Interlocked.Exchange(ref stopRequested, 1) == 0)
            {
               _logger.LogInformation("0002|Enter erkannt. FALCOM wird geordnet beendet.");
               _applicationLifetime.StopApplication();
            }
         }
         catch (Exception ex)
         {
            _logger.LogDebug(ex, "0003|Konsolen-Beenden per Enter ist nicht verfuegbar.");
         }
      }
   }
}
