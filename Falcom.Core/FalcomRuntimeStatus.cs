namespace Falcom;

public sealed class FalcomRuntimeStatus
{
   private readonly object sync = new();

   public bool OpcKranSpsVerbunden { get; private set; }
   public DateTime? OpcKranSpsStatusZeit { get; private set; }
   public string OpcKranSpsStatusText { get; private set; } = "Unbekannt";

   public bool OpcCwSpsVerbunden { get; private set; }
   public DateTime? OpcCwSpsStatusZeit { get; private set; }
   public string OpcCwSpsStatusText { get; private set; } = "Unbekannt";

   public bool OpcEOfenSpsVerbunden { get; private set; }
   public DateTime? OpcEOfenSpsStatusZeit { get; private set; }
   public string OpcEOfenSpsStatusText { get; private set; } = "Unbekannt";

   public int? LetzterWatchdogWert { get; private set; }
   public DateTime? LetzterWatchdogGesendetAm { get; private set; }
   public string WatchdogStatusText { get; private set; } = "Noch nicht gesendet";

   public int? LetzterSpsLebensZaehler { get; private set; }
   public DateTime? LetzterSpsLebensZaehlerEmpfangenAm { get; private set; }
   public string SpsLebensZaehlerStatusText { get; private set; } = "Noch nicht empfangen";
   public bool SpsLebensZaehlerGueltig { get; private set; }

   public int? LetzterCwLebensZaehler { get; private set; }
   public DateTime? LetzterCwLebensZaehlerEmpfangenAm { get; private set; }
   public string CwLebensZaehlerStatusText { get; private set; } = "Noch nicht empfangen";
   public bool CwLebensZaehlerGueltig { get; private set; }

   public int? LetzterEOfenLebensZaehler { get; private set; }
   public DateTime? LetzterEOfenLebensZaehlerEmpfangenAm { get; private set; }
   public string EOfenLebensZaehlerStatusText { get; private set; } = "Noch nicht empfangen";
   public bool EOfenLebensZaehlerGueltig { get; private set; }

   public long? AktuelleFahrtID { get; private set; }
   public long? AktuellerAuftragID { get; private set; }
   public string AktuellerAuftragsTyp { get; private set; } = string.Empty;
   public string AktuelleQuelle { get; private set; } = string.Empty;
   public string AktuellesZiel { get; private set; } = string.Empty;
   public decimal? AktuelleSollMengeKg { get; private set; }
   public DateTime? AktuelleFahrtAktualisiertAm { get; private set; }
   public DateTime? LetzteAuftragsPollerPruefungAm { get; private set; }
   public DateTime? LetzteFreigabePruefungAm { get; private set; }

   public void SetOpcKranSpsStatus(bool verbunden, string statusText)
   {
      lock (sync)
      {
         OpcKranSpsVerbunden = verbunden;
         OpcKranSpsStatusText = statusText;
         OpcKranSpsStatusZeit = DateTime.Now;
      }
   }

   public void SetOpcCwSpsStatus(bool verbunden, string statusText)
   {
      lock (sync)
      {
         OpcCwSpsVerbunden = verbunden;
         OpcCwSpsStatusText = statusText;
         OpcCwSpsStatusZeit = DateTime.Now;
      }
   }

   public void SetOpcEOfenSpsStatus(bool verbunden, string statusText)
   {
      lock (sync)
      {
         OpcEOfenSpsVerbunden = verbunden;
         OpcEOfenSpsStatusText = statusText;
         OpcEOfenSpsStatusZeit = DateTime.Now;
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
         SpsLebensZaehlerGueltig = true;
      }
   }

   public void SetSpsLebensZaehlerUnavailable(string statusText)
   {
      lock (sync)
      {
         SpsLebensZaehlerStatusText = statusText;
         SpsLebensZaehlerGueltig = false;
      }
   }

   public void SetCwLebensZaehlerReceived(int wert)
   {
      lock (sync)
      {
         LetzterCwLebensZaehler = wert;
         LetzterCwLebensZaehlerEmpfangenAm = DateTime.Now;
         CwLebensZaehlerStatusText = "Empfangen";
         CwLebensZaehlerGueltig = true;
         OpcCwSpsVerbunden = true;
         OpcCwSpsStatusText = "Datenfluss";
         OpcCwSpsStatusZeit = DateTime.Now;
      }
   }

   public void SetCwDataReceived(string statusText)
   {
      lock (sync)
      {
         OpcCwSpsVerbunden = true;
         OpcCwSpsStatusText = statusText;
         OpcCwSpsStatusZeit = DateTime.Now;
      }
   }

   public void SetCwLebensZaehlerUnavailable(string statusText)
   {
      lock (sync)
      {
         CwLebensZaehlerStatusText = statusText;
         CwLebensZaehlerGueltig = false;
         OpcCwSpsVerbunden = false;
         OpcCwSpsStatusText = statusText;
         OpcCwSpsStatusZeit = DateTime.Now;
      }
   }

   public void SetEOfenLebensZaehlerReceived(int wert)
   {
      lock (sync)
      {
         LetzterEOfenLebensZaehler = wert;
         LetzterEOfenLebensZaehlerEmpfangenAm = DateTime.Now;
         EOfenLebensZaehlerStatusText = "Empfangen";
         EOfenLebensZaehlerGueltig = true;
         OpcEOfenSpsVerbunden = true;
         OpcEOfenSpsStatusText = "Datenfluss";
         OpcEOfenSpsStatusZeit = DateTime.Now;
      }
   }

   public void SetEOfenLebensZaehlerUnavailable(string statusText)
   {
      lock (sync)
      {
         EOfenLebensZaehlerStatusText = statusText;
         EOfenLebensZaehlerGueltig = false;
         OpcEOfenSpsVerbunden = false;
         OpcEOfenSpsStatusText = statusText;
         OpcEOfenSpsStatusZeit = DateTime.Now;
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

   public void SetAuftragsPollerPruefung()
   {
      lock (sync)
      {
         LetzteAuftragsPollerPruefungAm = DateTime.Now;
      }
   }

   public void SetFreigabePruefung()
   {
      lock (sync)
      {
         LetzteFreigabePruefungAm = DateTime.Now;
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
            OpcCwSpsVerbunden,
            OpcCwSpsStatusZeit,
            OpcCwSpsStatusText,
            OpcEOfenSpsVerbunden,
            OpcEOfenSpsStatusZeit,
            OpcEOfenSpsStatusText,
            LetzterWatchdogWert,
            LetzterWatchdogGesendetAm,
            WatchdogStatusText,
            LetzterSpsLebensZaehler,
            LetzterSpsLebensZaehlerEmpfangenAm,
            SpsLebensZaehlerStatusText,
            SpsLebensZaehlerGueltig,
            LetzterCwLebensZaehler,
            LetzterCwLebensZaehlerEmpfangenAm,
            CwLebensZaehlerStatusText,
            CwLebensZaehlerGueltig,
            LetzterEOfenLebensZaehler,
            LetzterEOfenLebensZaehlerEmpfangenAm,
            EOfenLebensZaehlerStatusText,
            EOfenLebensZaehlerGueltig,
            AktuelleFahrtID,
            AktuellerAuftragID,
            AktuellerAuftragsTyp,
            AktuelleQuelle,
            AktuellesZiel,
            AktuelleSollMengeKg,
            AktuelleFahrtAktualisiertAm,
            LetzteAuftragsPollerPruefungAm,
            LetzteFreigabePruefungAm);
      }
   }
}

public sealed record FalcomRuntimeStatusSnapshot(
   bool OpcKranSpsVerbunden,
   DateTime? OpcKranSpsStatusZeit,
   string OpcKranSpsStatusText,
   bool OpcCwSpsVerbunden,
   DateTime? OpcCwSpsStatusZeit,
   string OpcCwSpsStatusText,
   bool OpcEOfenSpsVerbunden,
   DateTime? OpcEOfenSpsStatusZeit,
   string OpcEOfenSpsStatusText,
   int? LetzterWatchdogWert,
   DateTime? LetzterWatchdogGesendetAm,
   string WatchdogStatusText,
   int? LetzterSpsLebensZaehler,
   DateTime? LetzterSpsLebensZaehlerEmpfangenAm,
   string SpsLebensZaehlerStatusText,
   bool SpsLebensZaehlerGueltig,
   int? LetzterCwLebensZaehler,
   DateTime? LetzterCwLebensZaehlerEmpfangenAm,
   string CwLebensZaehlerStatusText,
   bool CwLebensZaehlerGueltig,
   int? LetzterEOfenLebensZaehler,
   DateTime? LetzterEOfenLebensZaehlerEmpfangenAm,
   string EOfenLebensZaehlerStatusText,
   bool EOfenLebensZaehlerGueltig,
   long? AktuelleFahrtID,
   long? AktuellerAuftragID,
   string AktuellerAuftragsTyp,
   string AktuelleQuelle,
   string AktuellesZiel,
   decimal? AktuelleSollMengeKg,
   DateTime? AktuelleFahrtAktualisiertAm,
   DateTime? LetzteAuftragsPollerPruefungAm,
   DateTime? LetzteFreigabePruefungAm);
