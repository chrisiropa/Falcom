using Microsoft.Extensions.Logging;
using System.Text;

namespace Falcom;

public sealed class FalcomConsoleSink
{
   private readonly object _sync = new();

   public FalcomConsoleSink()
   {
      try
      {
         if (OperatingSystem.IsWindows())
         {
            Console.OutputEncoding = Encoding.UTF8;
         }
      }
      catch
      {
      }
   }

   public void Write(LogLevel logLevel, string line)
   {
      lock (_sync)
      {
         try
         {
            var previousColor = Console.ForegroundColor;
            Console.ForegroundColor = GetColor(logLevel);
            Console.WriteLine(line);
            Console.ForegroundColor = previousColor;
         }
         catch
         {
         }
      }
   }

   private static ConsoleColor GetColor(LogLevel logLevel)
   {
      return logLevel switch
      {
         LogLevel.Trace => ConsoleColor.DarkGray,
         LogLevel.Debug => ConsoleColor.Cyan,
         LogLevel.Information => ConsoleColor.Gray,
         LogLevel.Warning => ConsoleColor.Yellow,
         LogLevel.Error => ConsoleColor.Red,
         LogLevel.Critical => ConsoleColor.Magenta,
         _ => Console.ForegroundColor
      };
   }
}
