using Microsoft.Extensions.Logging;

namespace Falcom;

public sealed class FalcomLogger : ILogger
{
   private readonly FalcomFileSink _fileSink;
   private readonly FalcomConsoleSink _consoleSink;
   private readonly FalcomUiLogSink _uiLogSink;

   public FalcomLogger(
      string _,
      FalcomFileSink fileSink,
      FalcomConsoleSink consoleSink,
      FalcomUiLogSink uiLogSink)
   {
      _fileSink = fileSink;
      _consoleSink = consoleSink;
      _uiLogSink = uiLogSink;
   }

   public IDisposable BeginScope<TState>(TState state) where TState : notnull
   {
      return NullScope.Instance;
   }

   public bool IsEnabled(LogLevel logLevel)
   {
      return logLevel != LogLevel.None;
   }

   public void Log<TState>(
      LogLevel logLevel,
      EventId eventId,
      TState state,
      Exception? exception,
      Func<TState, Exception?, string> formatter)
   {
      if (!IsEnabled(logLevel))
      {
         return;
      }

      var message = formatter(state, exception);
      if (string.IsNullOrWhiteSpace(message) && exception is null)
      {
         return;
      }

      var line = FormatLine(logLevel, message, exception);
      _uiLogSink.Write(logLevel, line);
      _fileSink.Write(line);
      _consoleSink.Write(logLevel, line);
   }

   private string FormatLine(LogLevel logLevel, string message, Exception? exception)
   {
      var timestamp = DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff");
      var levelText = ToShortLevelText(logLevel);
      var line = $"{timestamp} [{levelText}] {message}";

      if (exception is not null)
      {
         line = $"{line} | {FormatExceptionSummary(exception)}";
      }

      return line;
   }

   private static string FormatExceptionSummary(Exception exception)
   {
      List<string> parts = new();

      for (Exception? current = exception; current is not null; current = current.InnerException)
      {
         parts.Add($"{current.GetType().Name}: {current.Message}");
      }

      return string.Join(" | Inner: ", parts);
   }

   private static string ToShortLevelText(LogLevel logLevel)
   {
      return logLevel switch
      {
         LogLevel.Trace => "TRC",
         LogLevel.Debug => "DBG",
         LogLevel.Information => "INF",
         LogLevel.Warning => "WRN",
         LogLevel.Error => "ERR",
         LogLevel.Critical => "CRT",
         _ => "---"
      };
   }

   private sealed class NullScope : IDisposable
   {
      public static readonly NullScope Instance = new();

      public void Dispose()
      {
      }
   }
}
