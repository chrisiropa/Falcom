using Microsoft.Extensions.Logging;

namespace Falcom;

public sealed class FalcomLoggerProvider : ILoggerProvider
{
   private readonly FalcomFileSink _fileSink;
   private readonly FalcomConsoleSink _consoleSink;
   private readonly FalcomUiLogSink _uiLogSink;

   public FalcomLoggerProvider(
      FalcomFileSink fileSink,
      FalcomConsoleSink consoleSink,
      FalcomUiLogSink uiLogSink)
   {
      _fileSink = fileSink;
      _consoleSink = consoleSink;
      _uiLogSink = uiLogSink;
   }

   public ILogger CreateLogger(string categoryName)
   {
      return new FalcomLogger(categoryName, _fileSink, _consoleSink, _uiLogSink);
   }

   public void Dispose()
   {
   }
}
