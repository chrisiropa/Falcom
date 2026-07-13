namespace Falcom;

public sealed class FalcomKranLiveStatusService
{
   private readonly object syncRoot = new();
   private readonly Dictionary<int, KranOpcEventSnapshot> kranOpcEvents = new();

   private SpsLebensZaehlerSnapshot? spsLebensZaehler;
   private KranPositionSnapshot? kranPosition;

   public event Action<SpsLebensZaehlerSnapshot>? SpsLebensZaehlerChanged;
   public event Action<KranPositionSnapshot>? KranPositionChanged;
   public event Action<KranOpcEventSnapshot>? KranOpcEventChanged;

   public SpsLebensZaehlerSnapshot? GetSpsLebensZaehler()
   {
      lock (syncRoot)
      {
         return spsLebensZaehler;
      }
   }

   public KranPositionSnapshot? GetKranPosition()
   {
      lock (syncRoot)
      {
         return kranPosition;
      }
   }

   public IReadOnlyList<KranOpcEventSnapshot> GetKranOpcEvents()
   {
      lock (syncRoot)
      {
         return kranOpcEvents
            .Values
            .OrderBy(snapshot => snapshot.EventId)
            .ToArray();
      }
   }

   public KranOpcEventSnapshot? GetKranOpcEvent(int eventId)
   {
      lock (syncRoot)
      {
         return kranOpcEvents.TryGetValue(eventId, out KranOpcEventSnapshot? snapshot)
            ? snapshot
            : null;
      }
   }

   public SpsLebensZaehlerSnapshot SetSpsLebensZaehler(
      int lebensZaehler,
      DateTime timestampUtc,
      string source)
   {
      SpsLebensZaehlerSnapshot snapshot = new(
         lebensZaehler,
         timestampUtc,
         source);

      lock (syncRoot)
      {
         spsLebensZaehler = snapshot;
      }

      SpsLebensZaehlerChanged?.Invoke(snapshot);
      return snapshot;
   }

   public KranPositionSnapshot SetKranPosition(
      int? posKranX,
      int? posKatzeY,
      int? posHubZ,
      DateTime timestampUtc,
      string source)
   {
      KranPositionSnapshot snapshot = new(
         posKranX,
         posKatzeY,
         posHubZ,
         timestampUtc,
         source);

      lock (syncRoot)
      {
         kranPosition = snapshot;
      }

      KranPositionChanged?.Invoke(snapshot);
      return snapshot;
   }

   public KranOpcEventSnapshot SetKranOpcEvent(
      int eventId,
      string eventName,
      string direction,
      string triggerNodeName,
      object? triggerValue,
      IReadOnlyDictionary<string, object?> values,
      DateTime timestampUtc,
      string source)
   {
      KranOpcEventSnapshot snapshot = new(
         eventId,
         eventName,
         direction,
         triggerNodeName,
         Convert.ToString(triggerValue) ?? string.Empty,
         timestampUtc,
         source,
         values
            .Select(pair => new KranOpcEventItemSnapshot(
               pair.Key,
               Convert.ToString(pair.Value) ?? string.Empty))
            .ToArray());

      lock (syncRoot)
      {
         kranOpcEvents[eventId] = snapshot;
      }

      KranOpcEventChanged?.Invoke(snapshot);
      return snapshot;
   }
}

public sealed record SpsLebensZaehlerSnapshot(
   int LebensZaehler,
   DateTime TimestampUtc,
   string Source);

public sealed record KranPositionSnapshot(
   int? PosKranX,
   int? PosKatzeY,
   int? PosHubZ,
   DateTime TimestampUtc,
   string Source);

public sealed record KranOpcEventSnapshot(
   int EventId,
   string EventName,
   string Direction,
   string TriggerNodeName,
   string TriggerValue,
   DateTime TimestampUtc,
   string Source,
   IReadOnlyList<KranOpcEventItemSnapshot> Items);

public sealed record KranOpcEventItemSnapshot(
   string NodeName,
   string Value);
