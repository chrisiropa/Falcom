using System;
using System.Collections.Generic;
using System.Text;

namespace Falcom
{
   public enum ELF
   {
      INFO = 0x0001,
      WARNING = 0x0002,
      ERROR = 0x0004,
      DEVELOPER = 0x0008,
      SUCCESS = 0x0010,
      STATUS = 0x0020,

   }

   public struct LogEintrag
   {
      private ELF logFlags;
      private string text;
      private DateTime zeitStempel;
      private string threadInfo;

      public LogEintrag(ELF logFlags, string text, DateTime zeitStempel, string threadInfo)
      {
         this.logFlags = logFlags;
         this.text = text;
         this.zeitStempel = zeitStempel;
         this.threadInfo = threadInfo;
      }

      public ELF LogFlags
      {
         get { return logFlags; }
      }

      public string LogFlagAsText
      {
         get
         {
            switch (logFlags)
            {
               case ELF.INFO: return string.Format("INF|");
               case ELF.WARNING: return string.Format("WAR|");
               case ELF.DEVELOPER: return string.Format("DEV|");
               case ELF.ERROR: return string.Format("ERR|");
               case ELF.SUCCESS: return string.Format("SUC|");
            }
            return "UNKNOWN";
         }
      }

      public string Text
      {
         get { return text; }
      }

      public DateTime ZeitStempel
      {
         get { return zeitStempel; }
      }

      public string ThreadInfo
      {
         get { return threadInfo; }
      }

      public ConsoleColor GetColor(ELF elf)
      {
         switch (elf)
         {
            case ELF.STATUS: return ConsoleColor.DarkGreen;
            case ELF.DEVELOPER: return ConsoleColor.DarkCyan;
            case ELF.ERROR: return ConsoleColor.Red;
            case ELF.INFO: return Console.ForegroundColor;
            case ELF.SUCCESS: return ConsoleColor.Green;
            case ELF.WARNING: return ConsoleColor.Yellow;
         }

         return Console.ForegroundColor;
      }
   }
}
