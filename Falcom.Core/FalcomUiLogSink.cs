using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Falcom;

public sealed class FalcomUiLogSink
{
   private const int MaxEntries = 500;
   private readonly ConcurrentQueue<FalcomUiLogEntry> ablaufEntries = new();
   private readonly ConcurrentQueue<FalcomUiLogEntry> opcSendEntries = new();
   private readonly ConcurrentQueue<FalcomUiLogEntry> opcReceiveEntries = new();
   private long changeVersion;

   public long ChangeVersion => Volatile.Read(ref changeVersion);

   public void Write(LogLevel logLevel, string line)
   {
      FalcomUiLogEntry entry = new(DateTime.Now, logLevel, line);

      if (IsOpcSendLog(line))
      {
         EnqueueLimited(opcSendEntries, entry);
      }
      else if (IsOpcReceiveLog(line))
      {
         EnqueueLimited(opcReceiveEntries, entry);
      }
      else
      {
         EnqueueLimited(ablaufEntries, entry);
      }

      Interlocked.Increment(ref changeVersion);
   }

   public IReadOnlyList<FalcomUiLogEntry> SnapshotAblauf()
   {
      return ablaufEntries.ToArray();
   }

   public IReadOnlyList<FalcomUiLogEntry> SnapshotOpcSend()
   {
      return opcSendEntries.ToArray();
   }

   public IReadOnlyList<FalcomUiLogEntry> SnapshotOpcReceive()
   {
      return opcReceiveEntries.ToArray();
   }

   private static void EnqueueLimited(
      ConcurrentQueue<FalcomUiLogEntry> queue,
      FalcomUiLogEntry entry)
   {
      queue.Enqueue(entry);

      while (queue.Count > MaxEntries
             && queue.TryDequeue(out _))
      {
      }
   }

   private static bool IsOpcSendLog(string line)
   {
      return line.Contains("OPC Senden", StringComparison.OrdinalIgnoreCase)
             || line.Contains("OPC Schreiben", StringComparison.OrdinalIgnoreCase);
   }

   private static bool IsOpcReceiveLog(string line)
   {
      return line.Contains("OPC Empfang", StringComparison.OrdinalIgnoreCase)
             || line.Contains("OPC Event lesen", StringComparison.OrdinalIgnoreCase);
   }
}

public sealed record FalcomUiLogEntry(
   DateTime Timestamp,
   LogLevel LogLevel,
   string Line);
