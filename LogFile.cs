using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace Falcom
{
   class LogFile
   {
      private const long maxLogfileEntries = 200000; //Entspricht im Mittel 20MB Filegr��e
      private long logfileEntryCounter = 0;
      private bool firstError = true;
      private string logfilePath = "";

      public LogFile(string logfilePath)
      {
         this.logfilePath = logfilePath;

         try
         {
            FileStream fileStream = new FileStream(logfilePath, FileMode.Append, FileAccess.Write, FileShare.Write);
            fileStream.Close();
         }
         catch (Exception e)
         {
            Console.WriteLine("CONSOLE POS4: {0}", e.Message);
         }

         InitEntryCounter();
      }

      private void InitEntryCounter()
      {
         string thePath = logfilePath;

         if (File.Exists(thePath))
         {
            StreamReader reader = new StreamReader(thePath);

            logfileEntryCounter = 0;
            while (!reader.EndOfStream)
            {
               reader.ReadLine();
               logfileEntryCounter++;
            }
            reader.Close();
         }
         else
         {
            logfileEntryCounter = 0;
         }
      }


      private bool SetCurrentFileToHistoryFile()
      {
         string thePath = logfilePath;

         bool success = true;
         logfileEntryCounter = 0;

         try
         {
            if (File.Exists(thePath))
            {
               string historyFile = thePath.Insert(thePath.Length - 4, DateTime.Now.ToString(" ddMMyyyyHHmmssfff"));

               FileInfo fi = new FileInfo(thePath);
               fi.MoveTo(historyFile);
            }
         }
         catch (Exception e4)
         {
            success = false;

            Console.WriteLine("CONSOLE POS4: {0}", e4.Message);

         }

         return success;
      }

      public void Log(LogEintrag logEintrag)
      {
         string text = string.Format("{2} {0} {3} {1}", logEintrag.ZeitStempel.ToString("dd.MM.yy HH:mm:ss.fff "), logEintrag.Text, logEintrag.LogFlagAsText, logEintrag.ThreadInfo);


         bool written = false;
         long tryCounter = 0;

         string thePath = logfilePath;

         while ((!written) && (tryCounter < 10))
         {
            try
            {
               StreamWriter streamWriter = new StreamWriter(thePath, true, Encoding.UTF8);
               streamWriter.WriteLine(text);
               streamWriter.Close();
               written = true;
               logfileEntryCounter++;

               if ((logfileEntryCounter % 100) == 0)
               {
                  DeleteOldLogFiles();
               }

               if (logfileEntryCounter > maxLogfileEntries)
               {
                  if (!SetCurrentFileToHistoryFile())
                  {
                     logfileEntryCounter = 0;
                     streamWriter = new StreamWriter(thePath, true, Encoding.UTF8);
                     streamWriter.WriteLine(text);
                     streamWriter.Close();
                  }
               }
            }
            catch (Exception e5)
            {
               if (firstError)
               {
                  //Nicht die Konsole vollpflastern, wenn LogFile-Pfad in der Datenbank falsch 
                  //konfiguriert ist.
                  firstError = false;
                  Console.WriteLine("CONSOLE POS6: {0}", e5.Message);
               }

               tryCounter++;
               System.Threading.Thread.Sleep(0);
            }
         }
      }

      private void DeleteOldLogFiles()
      {
         //[ ] Alle 100 Eintr�ge das �lteste DatenClieNT-Logfile raussuchen 
         //[ ] Testen ob es �lter ist als 3 Monate 
         //[ ] Wenn ja l�schen    

         DirectoryInfo parent = new DirectoryInfo(Path.GetDirectoryName(logfilePath));

         List<FileInfo> children = new List<FileInfo>();

         foreach (FileInfo child in parent.GetFiles())
         {
            if (child.FullName.Contains(Path.GetFileNameWithoutExtension(logfilePath)))
            {
               children.Add(child);
            }
         }

         if (children.Count == 0)
         {
            return;
         }

         FileInfo oldest = children[0];

         foreach (FileInfo nextFile in children)
         {
            if (nextFile.LastWriteTimeUtc < oldest.LastWriteTimeUtc)
            {
               oldest = nextFile;
            }
         }


         TimeSpan timeSpan = DateTime.UtcNow - oldest.LastWriteTimeUtc;

         int keepDays = 120;


         if (timeSpan.TotalDays > keepDays)
         {
            //Nur l�schen wenn es nicht die Original aktuelle Logdatei ist !!!!!!!!!!!!!!!
            if (oldest.FullName.ToUpper() != logfilePath.ToUpper())
            {
               try
               {
                  File.Delete(oldest.FullName);
               }
               catch(Exception e)
               {
                  Console.WriteLine("CONSOLE POS7: {0}", e.Message);
               }
            }
         }

      }
   }
}
