using Microsoft.Data.SqlClient;
using System.Data;

namespace Falcom
{
   public sealed class LkwPlatzLeer207Event : FalcomEventBase
   {
      public const string EventName = "Event_207";
      public const string TriggerNodeName = EventName;
      public const string AuftragNummerNodeName = "Nr";
      public const string AuftragTeilfahrtNodeName = "TeilNr";
      public const string LkwPlatzNodeName = "LKWPlatz";

      public override string Source => "Kran-SPS";
      public override bool IsStateTrigger => true;

      public static string AenderungsZaehlerOPCNode { get; private set; } = string.Empty;
      public static string AuftragsNummerOPCNode { get; private set; } = string.Empty;
      public static string TeilfahrtIDOPCNode { get; private set; } = string.Empty;
      public static string LkwPlatzOPCNode { get; private set; } = string.Empty;

      public int AuftragsNummer { get; }
      public int TeilfahrtID { get; }
      public int LkwPlatzPositionID { get; }
      public int AenderungsZaehler { get; }

      public LkwPlatzLeer207Event(
         int auftragsNummer,
         int teilfahrtID,
         int lkwPlatzPositionID,
         int aenderungsZaehler)
      {
         AuftragsNummer = auftragsNummer;
         TeilfahrtID = teilfahrtID;
         LkwPlatzPositionID = lkwPlatzPositionID;
         AenderungsZaehler = aenderungsZaehler;
      }

      public static void LoadOpcNodes(ConfigManager configManager)
      {
         Dictionary<string, string> opcNodes = new(StringComparer.OrdinalIgnoreCase);

         using SqlConnection connection = new(configManager.ConnectionString);
         using SqlCommand command = new("dbo.FALCOM_GetEventOpcNodes", connection)
         {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 30
         };

         command.Parameters.Add("@EventName", SqlDbType.NVarChar, 128).Value = EventName;
         command.Parameters.Add("@Direction", SqlDbType.NVarChar, 64).Value = "KRAN_SPS->FALCOM";

         connection.Open();
         using SqlDataReader reader = command.ExecuteReader();
         while (reader.Read())
         {
            string nodeName = Convert.ToString(reader["NodeName"]) ?? string.Empty;
            string opcNode = Convert.ToString(reader["OPC_Node"]) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(nodeName))
            {
               opcNodes[nodeName] = opcNode;
            }
         }

         AenderungsZaehlerOPCNode = GetRequiredOpcNode(opcNodes, TriggerNodeName);
         AuftragsNummerOPCNode = GetRequiredOpcNode(opcNodes, AuftragNummerNodeName);
         TeilfahrtIDOPCNode = GetRequiredOpcNode(opcNodes, AuftragTeilfahrtNodeName);
         LkwPlatzOPCNode = GetRequiredOpcNode(opcNodes, LkwPlatzNodeName);
      }

      private static string GetRequiredOpcNode(
         IReadOnlyDictionary<string, string> opcNodes,
         string nodeName)
      {
         if (!opcNodes.TryGetValue(nodeName, out string? opcNode)
             || string.IsNullOrWhiteSpace(opcNode))
         {
            throw new InvalidOperationException(
               $"Fuer '{EventName}.{nodeName}' fehlt ein gueltiger Eintrag in der Event-Konfiguration.");
         }

         return opcNode.Trim();
      }
   }
}
