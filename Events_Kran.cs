namespace Falcom
{
   /// <summary> Signalisiert das Ende eines physischen Hubs </summary>
   public sealed class KranfahrtBeendetEvent : FalcomEventBase
   {
      public override string Source => "Kran-SPS";
      public override bool IsStateTrigger => true;
      public static string ÄnderungsZaehlerOPCNode { get; set; } = "KranSPS.KranfahrtBeendet.AenderungsZaehler";

      public int AuftragsNummer { get; init; }
      public int TeilfahrtID { get; init; }
      public string KranQuelle { get; init; }
      public string KranZiel { get; init; }
      public double Toleranz { get; init; }
      public double IstGewicht { get; init; }
      public int Fehlercode { get; init; }
      public int ÄnderungsZähler { get; init; }

      //Quelle und Ziel sind Werte aus FALCOM_KRAN_POSITION
      //1..3 LKW
      //11..20 Lagerboxen
      //100..103 Chargierwagen

      public KranfahrtBeendetEvent(int auftragsNummer, int teilfahrtID, string kranQuelle, string kranZiel, double toleranz, double istGewicht, int fehlercode, int änderungsZähler)
      {
         AuftragsNummer = auftragsNummer;
         TeilfahrtID = teilfahrtID;
         KranQuelle = kranQuelle;
         KranZiel = kranZiel;
         Toleranz = toleranz;
         IstGewicht = istGewicht;
         Fehlercode = fehlercode;
         ÄnderungsZähler = änderungsZähler;
      }
   }

   public sealed class KranfahrtGestartetEvent : FalcomEventBase
   {
      public override string Source => "Kran-SPS";
      public override bool IsStateTrigger => true;
      public static string ÄnderungsZaehlerOPCNode { get; set; } = "KranSPS.KranfahrtGestartet.AenderungsZaehler";

      public int AuftragsNummer { get; init; }
      public int TeilfahrtID { get; init; }
      public string KranQuelle { get; init; }
      public string KranZiel { get; init; }
      public double Toleranz { get; init; }
      public int ÄnderungsZähler { get; init; }

      //Quelle und Ziel sind Werte aus FALCOM_KRAN_POSITION
      //1..3 LKW
      //11..20 Lagerboxen
      //100..103 Chargierwagen

      public KranfahrtGestartetEvent(int auftragsNummer, int teilfahrtID, string kranQuelle, string kranZiel, double toleranz, int änderungsZähler)
      {
         //SPS meldet das der Kran losgefahren ist, nachdem er von Falcom die
         //Aufforderung dazu bekommen hat.
         //FALCON->KRAN Kranfahrt gestartet
         AuftragsNummer = auftragsNummer;
         TeilfahrtID = teilfahrtID;
         KranQuelle = kranQuelle;
         KranZiel = kranZiel;
         Toleranz = toleranz;
         ÄnderungsZähler = änderungsZähler;
      }
   }

   public sealed class KranfahrtStatusEvent : FalcomEventBase
   {
      public override string Source => "Kran-SPS";
      public override bool IsStateTrigger => false;
      public static string ÄnderungsZaehlerOPCNode { get; set; } = "KranSPS.KranfahrtStatus.AenderungsZaehler";

      public int AuftragsNummer { get; init; }
      public int TeilfahrtID { get; init; }
      public double XPos { get; init; }
      public double YPos { get; init; }
      public double ZPos { get; init; }
      public int Fahrtzeit { get; init; } // in ms
      public int ÄnderungsZähler { get; init; }

      public KranfahrtStatusEvent(int auftragsNummer, int teilfahrtID, double xPos, double yPos, double zPos, int fahrtzeit, int änderungsZähler)
      {
         AuftragsNummer = auftragsNummer;
         TeilfahrtID = teilfahrtID;
         XPos = xPos;
         YPos = yPos;
         ZPos = zPos;
         Fahrtzeit = fahrtzeit;
         ÄnderungsZähler = änderungsZähler;
      }
   }

   public sealed class LkwPlatzLeerEvent : FalcomEventBase
   {
      public override string Source => "Kran-SPS";
      public override bool IsStateTrigger => true;
      public static string ÄnderungsZaehlerOPCNode { get; set; } = "KranSPS.LkwPlatzLeer.AenderungsZaehler";

      public int LkwPlatzNr { get; init; }
      public int ÄnderungsZähler { get; init; }

      // Wertebereich für LkwPlatzNr analog zu den vorherigen Definitionen:
      // 1..3 LKW-Plätze

      public LkwPlatzLeerEvent(int lkwPlatzNr, int änderungsZähler)
      {
         LkwPlatzNr = lkwPlatzNr;
         ÄnderungsZähler = änderungsZähler;
      }
   }
}