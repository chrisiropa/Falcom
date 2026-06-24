using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Falcom;

public sealed class FalcomUiLogSink
{
   private const int MaxEntries = 500;
   private readonly ConcurrentQueue<FalcomUiLogEntry> entries = new();

   public void Write(LogLevel logLevel, string line)
   {
      entries.Enqueue(new FalcomUiLogEntry(DateTime.Now, logLevel, line));

      while (entries.Count > MaxEntries
             && entries.TryDequeue(out _))
      {
      }
   }

   public IReadOnlyList<FalcomUiLogEntry> Snapshot()
   {
      return entries.ToArray();
   }
}

public sealed record FalcomUiLogEntry(
   DateTime Timestamp,
   LogLevel LogLevel,
   string Line);
