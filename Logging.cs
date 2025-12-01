using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Falcom 
{
   public class Logging
   {
      private readonly ConfigManager _configManager;
      private readonly LogZentrale logZentrale; 
      private readonly LogFile logFile;
      private readonly LogConsole logConsole;

      private Boolean inf;
      private Boolean err;
      private Boolean war;
      private Boolean dev;
      
      public Logging(ConfigManager configManager)
      {
         _configManager = configManager;
         
         Init();
         
         logZentrale = new LogZentrale(inf, war, err, dev);
         logFile = new LogFile(_configManager.LogfilePath); 
         logConsole = new LogConsole();
         
         logZentrale.Register(logFile.Log);         
         logZentrale.Register(logConsole.Log);                
      }
      
      public void Init()
      {
         string info = "1";
         string error = "1";
         string warning = "1";
         string developer = "0";

         inf = info == "1";
         err = error == "1";
         war = warning == "1";
         dev = developer == "1";  
      }
      
      public void Run()
      {  
         logZentrale?.Start();
      }

      public void Stop()
      {  
         logZentrale?.Stop();
      }

      public void Log(LogEintrag logEintrag)
      {
         try
         {
            logZentrale?.Log(logEintrag);
         }
         catch(Exception e)
         {
            Console.WriteLine("Logging.Log Error = {0}", e.Message);
         }
      }

      public void ZLog(ELF logFlags, string formatString, params object[] paramObjects)
      {
         string threadInfo = string.Format("{0}:{1}", Thread.CurrentThread.Name, Thread.CurrentThread.ManagedThreadId);
         LogEintrag logEintrag = new LogEintrag(logFlags, string.Format(formatString, paramObjects), DateTime.Now, threadInfo);

         Log(logEintrag);
      }
   }
}
