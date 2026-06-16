using Microsoft.Extensions.Logging;

namespace Falcom;

public sealed class FalcomLoggerProvider : ILoggerProvider
{
   private readonly FalcomFileSink _fileSink;
   private readonly FalcomConsoleSink _consoleSink;

   public FalcomLoggerProvider(string logFilePath)
   {
      _fileSink = new FalcomFileSink(logFilePath);
      _consoleSink = new FalcomConsoleSink();
   }

   public ILogger CreateLogger(string categoryName)
   {
      return new FalcomLogger(categoryName, _fileSink, _consoleSink);
   }

   public void Dispose()
   {
   }
}
