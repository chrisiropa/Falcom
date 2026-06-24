using Microsoft.Extensions.Logging;

namespace Falcom;

public sealed class FalcomLogger : ILogger
{
   private readonly string _categoryName;
   private readonly FalcomFileSink _fileSink;
   private readonly FalcomConsoleSink _consoleSink;
   private readonly FalcomUiLogSink _uiLogSink;

   public FalcomLogger(
      string categoryName,
      FalcomFileSink fileSink,
      FalcomConsoleSink consoleSink,
      FalcomUiLogSink uiLogSink)
   {
      _categoryName = categoryName;
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
      _fileSink.Write(line);
      _consoleSink.Write(logLevel, line);
      _uiLogSink.Write(logLevel, line);
   }

   private string FormatLine(LogLevel logLevel, string message, Exception? exception)
   {
      var timestamp = DateTime.Now.ToString("dd.MM.yy HH:mm:ss.fff");
      var levelText = logLevel.ToString().ToUpperInvariant();
      var line = $"{timestamp} [{levelText}] {_categoryName}: {message}";

      if (exception is not null)
      {
         line = $"{line}{Environment.NewLine}{exception}";
      }

      return line;
   }

   private sealed class NullScope : IDisposable
   {
      public static readonly NullScope Instance = new();

      public void Dispose()
      {
      }
   }
}
