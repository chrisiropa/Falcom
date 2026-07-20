using Microsoft.Data.SqlClient;
using System.Data;

namespace Falcom
{
   public class Parameter
   {
      private readonly ConfigManager _configManager;
      private readonly ILogger<Parameter> _logger;

      public string OpcServer { get; } = string.Empty;

      public Parameter(ConfigManager configManager, ILogger<Parameter> logger)
      {
         _configManager = configManager;
         _logger = logger;

         try
         {
            OpcServer = LoadParameterValue("OpcServer");
         }
         catch (Exception e)
         {
            _logger.LogError(e, "0010|Datenbankparameter OpcServer konnte nicht geladen werden.");
         }
      }

      private string LoadParameterValue(string name)
      {
         using var connection = new SqlConnection(_configManager.ConnectionString);
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
            return string.Empty;
         }

         return Convert.ToString(reader["Wert"])?.Trim() ?? string.Empty;
      }
   }
}
