using Microsoft.Data.SqlClient;
using System.Data;
using System.IO;
using System.Text.Json;

namespace ChargierwagenSimulator;

internal sealed record SimulatorConfiguration(
   string ConnectionString,
   string OpcEndpoint,
   string LogfilePath,
   IReadOnlyList<EventConfiguration> Events,
   IReadOnlyList<EventMappingConfiguration> Zuordnungen);

internal sealed record EventConfiguration(
   long ID,
   string EventName,
   string Direction,
   IReadOnlyList<EventNodeConfiguration> Nodes);

internal sealed record EventNodeConfiguration(
   long ID,
   string NodeName,
   string NodeRole,
   string DataType,
   string OpcNode,
   string Beschreibung);

internal sealed record EventMappingConfiguration(
   long ID,
   long? SourceEventID,
   long? SourceNodeID,
   long TargetEventID,
   long TargetNodeID,
   string Zuordnungstyp,
   string? Fixwert,
   string? Info);

internal static class SimulatorConfig
{
   private const string Partner = "CW";
   private const string ZuordnungTableName = "dbo.FALCOM_CW_SIM_EventZuordnung";

   public static SimulatorConfiguration Load()
   {
      string settingsPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
      using JsonDocument document = JsonDocument.Parse(File.ReadAllText(settingsPath));
      JsonElement settings = document.RootElement.GetProperty("Appsettings");

      var builder = new SqlConnectionStringBuilder
      {
         DataSource = settings.GetProperty("Server").GetString(),
         InitialCatalog = settings.GetProperty("DatabaseName").GetString(),
         UserID = settings.GetProperty("User").GetString(),
         Password = settings.GetProperty("Password").GetString(),
         Encrypt = false
      };

      return new SimulatorConfiguration(
         builder.ConnectionString,
         LoadParameterValue(builder.ConnectionString, "CWOpcServer", LoadParameterValue(builder.ConnectionString, "OpcServer", "opc.tcp://localhost:4840")),
         LoadLogfilePath(settings),
         LoadEvents(builder.ConnectionString),
         LoadZuordnungen(builder.ConnectionString));
   }

   private static string LoadLogfilePath(JsonElement settings)
   {
      if (settings.TryGetProperty("ChargierwagenSimulationLogfilePath", out JsonElement configuredPath))
      {
         string? value = configuredPath.GetString();
         if (!string.IsNullOrWhiteSpace(value))
         {
            return value.Trim();
         }
      }

      return @"C:\Projekte\LOGS\FALCOM\Chargierwagen_Simulation\Chargierwagen_Simulation.log";
   }

   private static IReadOnlyList<EventConfiguration> LoadEvents(string connectionString)
   {
      var events = new Dictionary<long, (string EventName, string Direction, List<EventNodeConfiguration> Nodes)>();

      using var connection = new SqlConnection(connectionString);
      using var command = new SqlCommand(
         """
         SELECT
            e.ID AS EventID,
            e.EventName,
            e.Direction,
            n.ID AS NodeID,
            n.NodeName,
            n.NodeRole,
            COALESCE(n.DataType, N'') AS DataType,
            n.OPC_Node,
            COALESCE(n.Beschreibung, N'') AS Beschreibung
         FROM dbo.FALCOM_EVENTS AS e
         LEFT JOIN dbo.FALCOM_EVENT_OPC_NODES AS n
            ON n.EventID = e.ID
         WHERE e.Partner = @Partner
           AND e.IsActive = 1
         ORDER BY e.ID, n.ID;
         """,
         connection);

      command.CommandType = CommandType.Text;
      command.CommandTimeout = 10;
      command.Parameters.Add("@Partner", SqlDbType.NVarChar, 30).Value = Partner;

      connection.Open();
      using SqlDataReader reader = command.ExecuteReader();
      while (reader.Read())
      {
         long eventId = Convert.ToInt64(reader["EventID"]);
         if (!events.TryGetValue(eventId, out var current))
         {
            current = (
               Convert.ToString(reader["EventName"]) ?? string.Empty,
               Convert.ToString(reader["Direction"]) ?? string.Empty,
               new List<EventNodeConfiguration>());
            events[eventId] = current;
         }

         if (reader["NodeID"] is not DBNull)
         {
            current.Nodes.Add(new EventNodeConfiguration(
               Convert.ToInt64(reader["NodeID"]),
               Convert.ToString(reader["NodeName"]) ?? string.Empty,
               Convert.ToString(reader["NodeRole"]) ?? string.Empty,
               Convert.ToString(reader["DataType"]) ?? string.Empty,
               Convert.ToString(reader["OPC_Node"]) ?? string.Empty,
               Convert.ToString(reader["Beschreibung"]) ?? string.Empty));
         }
      }

      return events
         .Select(x => new EventConfiguration(x.Key, x.Value.EventName, x.Value.Direction, x.Value.Nodes))
         .ToList();
   }

   private static IReadOnlyList<EventMappingConfiguration> LoadZuordnungen(string connectionString)
   {
      var result = new List<EventMappingConfiguration>();

      using var connection = new SqlConnection(connectionString);
      using var command = new SqlCommand(
         $"""
         IF OBJECT_ID(N'{ZuordnungTableName}', N'U') IS NOT NULL
         BEGIN
            SELECT ID, SourceEventID, SourceNodeID, TargetEventID, TargetNodeID, Zuordnungstyp, Fixwert, Info
            FROM {ZuordnungTableName}
            WHERE Aktiv = 1
            ORDER BY ID;
         END
         """,
         connection);

      command.CommandType = CommandType.Text;
      command.CommandTimeout = 10;

      connection.Open();
      using SqlDataReader reader = command.ExecuteReader();
      while (reader.Read())
      {
         result.Add(new EventMappingConfiguration(
            Convert.ToInt64(reader["ID"]),
            reader["SourceEventID"] is DBNull ? null : Convert.ToInt64(reader["SourceEventID"]),
            reader["SourceNodeID"] is DBNull ? null : Convert.ToInt64(reader["SourceNodeID"]),
            Convert.ToInt64(reader["TargetEventID"]),
            Convert.ToInt64(reader["TargetNodeID"]),
            Convert.ToString(reader["Zuordnungstyp"]) ?? string.Empty,
            reader["Fixwert"] is DBNull ? null : Convert.ToString(reader["Fixwert"]),
            reader["Info"] is DBNull ? null : Convert.ToString(reader["Info"])));
      }

      return result;
   }

   private static string LoadParameterValue(
      string connectionString,
      string name,
      string fallback)
   {
      using var connection = new SqlConnection(connectionString);
      using var command = new SqlCommand("dbo.FALCOM_GetParameterValue", connection)
      {
         CommandType = CommandType.StoredProcedure,
         CommandTimeout = 10
      };
      command.Parameters.Add("@Name", SqlDbType.NVarChar, 256).Value = name;

      connection.Open();
      using SqlDataReader reader = command.ExecuteReader();
      if (!reader.Read())
      {
         return fallback;
      }

      string? value = Convert.ToString(reader["Wert"]);
      return string.IsNullOrWhiteSpace(value)
         ? fallback
         : value.Trim();
   }
}
