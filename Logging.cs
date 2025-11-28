using System;
using System.Collections.Generic;
using System.Text;

namespace Falcom 
{
   public class Logging
   {
      private readonly ConfigManager _configManager;
      private LogZentrale logZentrale; 
      private LogFile logFile;
      private LogConsole logConsole;

      private Boolean inf;
      private Boolean err;
      private Boolean war;
      private Boolean dev;
      
      public Logging(ConfigManager configManager)
      {
         _configManager = configManager;
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
         Init();
         
         logZentrale = new LogZentrale(inf, war, err, dev);
         logZentrale.Start();
         
         logFile = new LogFile(_configManager.LogfilePath); 
         
         logZentrale.Register(logFile.Log);         
         
         logConsole = new LogConsole();
         logZentrale.Register(logConsole.Log);                
      }

      public void Stop()
      {  
         logZentrale.Stop();
      }

      public void Log(LogEintrag logEintrag)
      {
         try
         {
            logZentrale.Log(logEintrag);
         }
         catch(Exception e)
         {
            Console.WriteLine("Logging.Log Error = {0}", e.Message);
         }
      }
   }
}
