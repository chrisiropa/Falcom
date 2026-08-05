using Microsoft.Data.SqlClient;
using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;

namespace Falcom;

public sealed class FalcomEventLogRepository
{
   private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
   {
      WriteIndented = false
   };

   private readonly ConfigManager _configManager;
   private readonly ILogger<FalcomEventLogRepository> _logger;
   private readonly ConcurrentDictionary<string, IReadOnlySet<string>> _logValueNodeNamesByEvent = new(StringComparer.OrdinalIgnoreCase);

   public FalcomEventLogRepository(
      ConfigManager configManager,
      ILogger<FalcomEventLogRepository> logger)
   {
      _configManager = configManager;
      _logger = logger;
   }

   public void LogEvent(
      string eventName,
      string direction,
      int? eventNumber,
      IReadOnlyDictionary<string, object?>? payload = null,
      bool filterPayloadByLogValue = true)
   {
      try
      {
         IReadOnlyDictionary<string, object?>? payloadToWrite = payload;
         if (filterPayloadByLogValue && payload is not null)
         {
            IReadOnlySet<string> logValueNodeNames = GetLogValueNodeNames(eventName, direction);
            payloadToWrite = payload
               .Where(item => logValueNodeNames.Contains(item.Key))
               .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
         }

         string? payloadJson = payloadToWrite is null || payloadToWrite.Count == 0
            ? null
            : JsonSerializer.Serialize(payloadToWrite, JsonOptions);

         (string quelle, string ziel) = ResolveQuelleZiel(direction);

         using SqlConnection connection = new(_configManager.ConnectionString);
         using SqlCommand command = new(
            """
            INSERT INTO dbo.FALCOM_EVENT_LOG
               (Richtung, Quelle, Ziel, EventName, EventNummer, PayloadJson)
            VALUES
               (@Richtung, @Quelle, @Ziel, @EventName, @EventNummer, @PayloadJson);
            """,
            connection)
         {
            CommandType = CommandType.Text,
            CommandTimeout = 10
         };

         command.Parameters.Add("@Richtung", SqlDbType.NVarChar, 64).Value = direction;
         command.Parameters.Add("@Quelle", SqlDbType.NVarChar, 32).Value = quelle;
         command.Parameters.Add("@Ziel", SqlDbType.NVarChar, 32).Value = ziel;
         command.Parameters.Add("@EventName", SqlDbType.NVarChar, 128).Value = eventName;
         command.Parameters.Add("@EventNummer", SqlDbType.Int).Value = eventNumber.HasValue ? eventNumber.Value : DBNull.Value;
         command.Parameters.Add("@PayloadJson", SqlDbType.NVarChar, -1).Value = payloadJson is null ? DBNull.Value : payloadJson;

         connection.Open();
         command.ExecuteNonQuery();
      }
      catch (Exception ex)
      {
         _logger.LogWarning(
            ex,
            "01FF|FALCOM_EVENT_LOG konnte nicht geschrieben werden. Event={EventName}, Direction={Direction}, EventNummer={EventNummer}.",
            eventName,
            direction,
            eventNumber);
      }
   }

   private IReadOnlySet<string> GetLogValueNodeNames(string eventName, string direction)
   {
      string key = eventName + "|" + direction;
      return _logValueNodeNamesByEvent.GetOrAdd(
         key,
         _ => LoadLogValueNodeNames(eventName, direction));
   }

   private IReadOnlySet<string> LoadLogValueNodeNames(string eventName, string direction)
   {
      var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

      using SqlConnection connection = new(_configManager.ConnectionString);
      using SqlCommand command = new(
         """
         SELECT nodes.NodeName
         FROM dbo.FALCOM_EVENTS AS events
         INNER JOIN dbo.FALCOM_EVENT_OPC_NODES AS nodes
            ON nodes.EventID = events.ID
         WHERE events.EventName = @EventName
           AND events.Direction = @Direction
           AND nodes.LogValue = 1;
         """,
         connection)
      {
         CommandType = CommandType.Text,
         CommandTimeout = 10
      };

      command.Parameters.Add("@EventName", SqlDbType.NVarChar, 128).Value = eventName;
      command.Parameters.Add("@Direction", SqlDbType.NVarChar, 64).Value = direction;

      connection.Open();
      using SqlDataReader reader = command.ExecuteReader();
      while (reader.Read())
      {
         string nodeName = Convert.ToString(reader["NodeName"])?.Trim() ?? string.Empty;
         if (!string.IsNullOrWhiteSpace(nodeName))
         {
            result.Add(nodeName);
         }
      }

      return result;
   }

   private static (string Quelle, string Ziel) ResolveQuelleZiel(string direction)
   {
      return direction.Trim().ToUpperInvariant() switch
      {
         "FALCOM->KRAN_SPS" => ("FALCOM", "KRAN"),
         "KRAN_SPS->FALCOM" => ("KRAN", "FALCOM"),
         "FALCOM->JK" => ("FALCOM", "CW"),
         "JK->FALCOM" => ("CW", "FALCOM"),
         "FALCOM->ABP" => ("FALCOM", "E_OFEN"),
         "ABP->FALCOM" => ("E_OFEN", "FALCOM"),
         _ => ("FALCOM", "UNBEKANNT")
      };
   }
}
