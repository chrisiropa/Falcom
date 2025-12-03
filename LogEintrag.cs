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

   public readonly struct LogEintrag(ELF logFlags, string text, DateTime zeitStempel, string threadInfo)
   {
      private readonly ELF logFlags = logFlags;
      private readonly string text = text;
      private readonly DateTime zeitStempel = zeitStempel;
      private readonly string threadInfo = threadInfo;

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
               case ELF.STATUS: return string.Format("STS|");
               default: return string.Format("UNKNOWN|");
            }
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

      public static ConsoleColor GetColor(ELF elf)
      {
         switch (elf)
         {
            case ELF.STATUS: return ConsoleColor.DarkGreen;
            case ELF.DEVELOPER: return ConsoleColor.DarkCyan;
            case ELF.ERROR: return ConsoleColor.Red;
            case ELF.INFO: return Console.ForegroundColor;
            case ELF.SUCCESS: return ConsoleColor.Green;
            case ELF.WARNING: return ConsoleColor.Yellow;
            default: break;
         }

         return Console.ForegroundColor;
      }
   }
}
