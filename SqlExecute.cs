using Microsoft.Data.SqlClient;
using System;

namespace Falcom
{
   public class SqlExecute
   {
      private readonly string stmt;
      private int affectedRows = -1;
      private readonly ConfigManager _configManager;

      public int AffectedRows
      {
         get
         {
            return affectedRows;
         }
      }

      public SqlExecute(ConfigManager configManager, string formatString, params object[] paramObjects)
      {
         _configManager = configManager;
         this.stmt = string.Format(formatString, paramObjects);

         Execute();
      }

      private void Execute()
      {
         using (SqlConnection sqlConnection = new SqlConnection(_configManager.ConnectionString))
         {
            try
            {
               sqlConnection.Open();
               SqlCommand dataCommand = new SqlCommand();
               dataCommand.Connection = sqlConnection;
               dataCommand.CommandTimeout = 30;
               dataCommand.CommandText = stmt;
               affectedRows = dataCommand.ExecuteNonQuery();

            }
            catch (Exception e)
            {
               throw new Exception(string.Format("SqlExecute -> Fehler beim Ausführen des Statements: #{0}# -> {1} ", stmt, e.Message));
            }
         }
      }
   }
}
