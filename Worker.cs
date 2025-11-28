namespace Falcom
{
   public class Worker(ILogger<Worker> logger, ConfigManager configManager) : BackgroundService
   {
      public static Worker TheFALOCOM;


      public Parameter Parameter
      {
         get { return parameter; }
      }

      private Logging logging = new Logging(configManager);
      private Parameter parameter = new Parameter(configManager);
      private Lager lager = new Lager(configManager);

      public Lager Lager
      {
         get
         {
            return lager;
         }
      }

      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
         TheFALOCOM = this;

         logging.Run();

         
         parameter.Init();
         lager.Update();

         while (!stoppingToken.IsCancellationRequested)
         {
            if (logger.IsEnabled(LogLevel.Information))
            {
               logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }

            Optimizer opti = new Optimizer();

            opti.Run();


            await Task.Delay(1000, stoppingToken);
         }


         logging.Stop();
      }

      public void ZLog(ELF logFlags, string formatString, params object[] paramObjects)
      {
         string threadInfo = string.Format("{0}:{1}", Thread.CurrentThread.Name, Thread.CurrentThread.ManagedThreadId);
         LogEintrag logEintrag = new LogEintrag(logFlags, string.Format(formatString, paramObjects), DateTime.Now, threadInfo);

         logging.Log(logEintrag);
      }
   }
}
