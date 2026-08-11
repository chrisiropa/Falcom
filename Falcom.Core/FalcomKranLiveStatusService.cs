namespace Falcom;

public sealed class FalcomKranLiveStatusService
{
   private readonly object syncRoot = new();
   private readonly Dictionary<int, KranOpcEventSnapshot> kranOpcEvents = new();

   private SpsLebensZaehlerSnapshot? spsLebensZaehler;
   private KranPositionSnapshot? kranPosition;
   private CwIstgewichteSnapshot? cwIstgewichte;
   private CwStoerungenSnapshot? cwStoerungen;

   public event Action<SpsLebensZaehlerSnapshot>? SpsLebensZaehlerChanged;
   public event Action<KranPositionSnapshot>? KranPositionChanged;
   public event Action<CwIstgewichteSnapshot>? CwIstgewichteChanged;
   public event Action<CwStoerungenSnapshot>? CwStoerungenChanged;
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

   public CwIstgewichteSnapshot? GetCwIstgewichte()
   {
      lock (syncRoot)
      {
         return cwIstgewichte;
      }
   }

   public CwStoerungenSnapshot? GetCwStoerungen()
   {
      lock (syncRoot)
      {
         return cwStoerungen;
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
      int? magnetAn,
      int? masseNetto,
      DateTime timestampUtc,
      string source)
   {
      KranPositionSnapshot snapshot = new(
         posKranX,
         posKatzeY,
         posHubZ,
         magnetAn,
         masseNetto,
         timestampUtc,
         source);

      lock (syncRoot)
      {
         kranPosition = snapshot;
      }

      KranPositionChanged?.Invoke(snapshot);
      return snapshot;
   }

   public CwIstgewichteSnapshot SetCwIstgewichte(
      int? istgewChW1,
      int? istgewChW2,
      int? istgewChW3,
      DateTime timestampUtc,
      string source)
   {
      CwIstgewichteSnapshot snapshot = new(
         istgewChW1,
         istgewChW2,
         istgewChW3,
         timestampUtc,
         source);

      lock (syncRoot)
      {
         cwIstgewichte = snapshot;
      }

      CwIstgewichteChanged?.Invoke(snapshot);
      return snapshot;
   }

   public CwStoerungenSnapshot SetCwStoerungen(
      bool? stoerungChW1,
      bool? stoerungChW2,
      bool? stoerungChW3,
      DateTime timestampUtc,
      string source)
   {
      CwStoerungenSnapshot snapshot = new(
         stoerungChW1,
         stoerungChW2,
         stoerungChW3,
         timestampUtc,
         source);

      lock (syncRoot)
      {
         cwStoerungen = snapshot;
      }

      CwStoerungenChanged?.Invoke(snapshot);
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
   int? PosKran,
   int? PosKatze,
   int? PosHub,
   int? MagnetAn,
   int? MasseNetto,
   DateTime TimestampUtc,
   string Source);

public sealed record CwIstgewichteSnapshot(
   int? IstgewChW1,
   int? IstgewChW2,
   int? IstgewChW3,
   DateTime TimestampUtc,
   string Source);

public sealed record CwStoerungenSnapshot(
   bool? StoerungChW1,
   bool? StoerungChW2,
   bool? StoerungChW3,
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

