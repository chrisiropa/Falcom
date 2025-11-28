using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Falcom
{
   
   public class LogZentrale
   {
      private Thread dispatcherThread;
      private volatile bool terminate = false;
      private Queue logEintraege;
      private event WriteDelegate writeEvent;
      
      private Boolean inf, war, err, dev;


      public LogZentrale(Boolean inf, Boolean war, Boolean err, Boolean dev)
      {
         logEintraege = Queue.Synchronized(new Queue(50000));
         
         this.inf = inf;
         this.war = war;
         this.err = err;
         this.dev = dev;
      }

      public void Start()
      {
         dispatcherThread = new Thread(new ThreadStart(DispatcherThreadFunc));
         dispatcherThread.Start();
      }

      public void Stop()
      {
         try
         {
            terminate = true;
            lock (logEintraege)
            {
               Monitor.PulseAll(logEintraege);
            }
            dispatcherThread.Join();
         }
         catch
         {
            Environment.Exit(0);
         }
      }

      private void DispatcherThreadFunc()
      {
         LogEintrag logEintrag;

         while (!terminate)
         {
            while ((logEintraege.Count != 0) && (!terminate))
            {
               try
               {
                  logEintrag = (LogEintrag)logEintraege.Dequeue();

                  Dispatch(logEintrag);
               }
               catch (Exception e)
               {
                  Console.WriteLine("CONSOLE POS8: {0}", e.Message);
               }
            }

            if ((!terminate) && (logEintraege.Count == 0))
            {
               lock (logEintraege)
               {
                  if (logEintraege.Count == 0)
                  {
                     Monitor.Wait(logEintraege);
                  }
               }
            }
         }
         while (logEintraege.Count != 0)
         {
            try
            {
               logEintrag = (LogEintrag)logEintraege.Dequeue();

               Dispatch(logEintrag);
            }
            catch(Exception e)
            {
               Console.WriteLine("CONSOLE POS10: {0}", e.Message);
            }
         }

         dispatcherThread = null;

      }

      public void Log(LogEintrag logEintrag)
      {
         
         if((logEintrag.LogFlags == ELF.INFO) && (!(inf)))
         {
            return;
         }
         else if ((logEintrag.LogFlags == ELF.WARNING) && (!(war)))
         {
            return;
         }
         else if ((logEintrag.LogFlags == ELF.ERROR) && (!(err)))
         {
            return;
         }
         else if ((logEintrag.LogFlags == ELF.DEVELOPER) && (!(dev)))
         {
            return;
         }
         
         logEintraege.Enqueue(logEintrag);

         lock (logEintraege)
         {
            Monitor.PulseAll(logEintraege);
         }
      }

      private void Dispatch(LogEintrag logEintrag)
      {
         if(writeEvent != null)
         {
            //F�r alle registrierten Member wird deren Write Funktion aufgerufen
            //Aufruf aus DispatcherThreadFunc
            writeEvent(logEintrag);
         }
      }

      public void Register(WriteDelegate writeDelegate)
      {
         writeEvent += writeDelegate;
      }
   }
}
