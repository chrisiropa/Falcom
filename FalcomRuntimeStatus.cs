namespace Falcom;

public sealed class FalcomRuntimeStatus
{
   private readonly object sync = new();

   public bool OpcKranSpsVerbunden { get; private set; }
   public DateTime? OpcKranSpsStatusZeit { get; private set; }
   public string OpcKranSpsStatusText { get; private set; } = "Unbekannt";

   public int? LetzterWatchdogWert { get; private set; }
   public DateTime? LetzterWatchdogGesendetAm { get; private set; }
   public string WatchdogStatusText { get; private set; } = "Noch nicht gesendet";

   public int? LetzterSpsLebensZaehler { get; private set; }
   public DateTime? LetzterSpsLebensZaehlerEmpfangenAm { get; private set; }
   public string SpsLebensZaehlerStatusText { get; private set; } = "Noch nicht empfangen";

   public long? AktuelleFahrtID { get; private set; }
   public long? AktuellerAuftragID { get; private set; }
   public string AktuellerAuftragsTyp { get; private set; } = string.Empty;
   public string AktuelleQuelle { get; private set; } = string.Empty;
   public string AktuellesZiel { get; private set; } = string.Empty;
   public decimal? AktuelleSollMengeKg { get; private set; }
   public DateTime? AktuelleFahrtAktualisiertAm { get; private set; }

   public void SetOpcKranSpsStatus(bool verbunden, string statusText)
   {
      lock (sync)
      {
         OpcKranSpsVerbunden = verbunden;
         OpcKranSpsStatusText = statusText;
         OpcKranSpsStatusZeit = DateTime.Now;
      }
   }

   public void SetWatchdogSent(int wert)
   {
      lock (sync)
      {
         LetzterWatchdogWert = wert;
         LetzterWatchdogGesendetAm = DateTime.Now;
         WatchdogStatusText = "Gesendet";
      }
   }

   public void SetWatchdogError(string statusText)
   {
      lock (sync)
      {
         WatchdogStatusText = statusText;
      }
   }

   public void SetSpsLebensZaehlerReceived(int wert)
   {
      lock (sync)
      {
         LetzterSpsLebensZaehler = wert;
         LetzterSpsLebensZaehlerEmpfangenAm = DateTime.Now;
         SpsLebensZaehlerStatusText = "Empfangen";
      }
   }

   public void SetAktuelleFahrt(AktuelleFahrtResult aktuelleFahrt)
   {
      lock (sync)
      {
         AktuelleFahrtID = aktuelleFahrt.AktuelleFahrtID;
         AktuellerAuftragID = aktuelleFahrt.AuftragID;
         AktuellerAuftragsTyp = aktuelleFahrt.AuftragsTyp;
         AktuelleQuelle = aktuelleFahrt.Quelle;
         AktuellesZiel = aktuelleFahrt.Ziel;
         AktuelleSollMengeKg = aktuelleFahrt.SollMengeKg;
         AktuelleFahrtAktualisiertAm = DateTime.Now;
      }
   }

   public FalcomRuntimeStatusSnapshot Snapshot()
   {
      lock (sync)
      {
         return new FalcomRuntimeStatusSnapshot(
            OpcKranSpsVerbunden,
            OpcKranSpsStatusZeit,
            OpcKranSpsStatusText,
            LetzterWatchdogWert,
            LetzterWatchdogGesendetAm,
            WatchdogStatusText,
            LetzterSpsLebensZaehler,
            LetzterSpsLebensZaehlerEmpfangenAm,
            SpsLebensZaehlerStatusText,
            AktuelleFahrtID,
            AktuellerAuftragID,
            AktuellerAuftragsTyp,
            AktuelleQuelle,
            AktuellesZiel,
            AktuelleSollMengeKg,
            AktuelleFahrtAktualisiertAm);
      }
   }
}

public sealed record FalcomRuntimeStatusSnapshot(
   bool OpcKranSpsVerbunden,
   DateTime? OpcKranSpsStatusZeit,
   string OpcKranSpsStatusText,
   int? LetzterWatchdogWert,
   DateTime? LetzterWatchdogGesendetAm,
   string WatchdogStatusText,
   int? LetzterSpsLebensZaehler,
   DateTime? LetzterSpsLebensZaehlerEmpfangenAm,
   string SpsLebensZaehlerStatusText,
   long? AktuelleFahrtID,
   long? AktuellerAuftragID,
   string AktuellerAuftragsTyp,
   string AktuelleQuelle,
   string AktuellesZiel,
   decimal? AktuelleSollMengeKg,
   DateTime? AktuelleFahrtAktualisiertAm);
