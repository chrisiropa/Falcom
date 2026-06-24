namespace Falcom
{
   /// <summary> Signalisiert das Ende eines physischen Hubs </summary>
   public sealed class KranfahrtBeendetEvent : FalcomEventBase
   {
      private const string EventName = "KranfahrtBeendet";

      public override string Source => "Kran-SPS";
      public override bool IsStateTrigger => true;

      public static string ÄnderungsZaehlerOPCNode { get; private set; } = string.Empty;
      public static string AuftragsNummerOPCNode { get; private set; } = string.Empty;
      public static string TeilfahrtIDOPCNode { get; private set; } = string.Empty;
      public static string KranQuelleOPCNode { get; private set; } = string.Empty;
      public static string KranZielOPCNode { get; private set; } = string.Empty;
      public static string ToleranzOPCNode { get; private set; } = string.Empty;
      public static string IstGewichtOPCNode { get; private set; } = string.Empty;
      public static string FehlercodeOPCNode { get; private set; } = string.Empty;

      public int AuftragsNummer { get; init; }
      public int TeilfahrtID { get; init; }
      public string KranQuelle { get; init; }
      public string KranZiel { get; init; }
      public double Toleranz { get; init; }
      public double IstGewicht { get; init; }
      public int Fehlercode { get; init; }
      public int ÄnderungsZähler { get; init; }

      public static void LoadOpcNodes(ConfigManager configManager)
      {
         SimpleSqlQuery query = new(
            configManager.ConnectionString,
            $"""
            SELECT nodes.NodeName, nodes.OPC_Node
            FROM dbo.FALCOM_EVENTS AS events
            INNER JOIN dbo.FALCOM_EVENT_OPC_NODES AS nodes
               ON nodes.EventID = events.ID
            WHERE events.EventName = '{EventName}'
              AND events.Direction = 'KRAN_SPS->FALCOM'
              AND events.IsActive = 1
            """);

         if (query.Exception is not null)
         {
            throw new InvalidOperationException(
               "Die OPC-Nodes für KranfahrtBeendet konnten nicht aus der Event-Konfiguration geladen werden.",
               query.Exception);
         }

         Dictionary<string, string> opcNodes = query.QueryResult?
            .ToDictionary(
               row => Convert.ToString(row["NodeName"]) ?? string.Empty,
               row => Convert.ToString(row["OPC_Node"]) ?? string.Empty,
               StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

         ÄnderungsZaehlerOPCNode = GetRequiredOpcNode(opcNodes, "AenderungsZaehler");
         AuftragsNummerOPCNode = GetRequiredOpcNode(opcNodes, "AuftragsNummer");
         TeilfahrtIDOPCNode = GetRequiredOpcNode(opcNodes, "AuftragTeilfahrt");
         KranQuelleOPCNode = GetRequiredOpcNode(opcNodes, "KranQuelle");
         KranZielOPCNode = GetRequiredOpcNode(opcNodes, "KranZiel");
         ToleranzOPCNode = GetRequiredOpcNode(opcNodes, "Toleranz");
         IstGewichtOPCNode = GetRequiredOpcNode(opcNodes, "IstGewicht");
         FehlercodeOPCNode = GetRequiredOpcNode(opcNodes, "Fehlercode");
      }

      private static string GetRequiredOpcNode(
         IReadOnlyDictionary<string, string> opcNodes,
         string nodeName)
      {
         if (!opcNodes.TryGetValue(nodeName, out string? opcNode)
             || string.IsNullOrWhiteSpace(opcNode))
         {
            throw new InvalidOperationException(
               $"Für '{EventName}.{nodeName}' fehlt ein gültiger Eintrag in der Event-Konfiguration.");
         }

         return opcNode.Trim();
      }

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
