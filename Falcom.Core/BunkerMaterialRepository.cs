using Microsoft.Data.SqlClient;
using System.Data;

namespace Falcom
{
   public sealed class BunkerMaterialRepository
   {
      private readonly ConfigManager _configManager;

      public BunkerMaterialRepository(ConfigManager configManager)
      {
         _configManager = configManager;
      }

      public BunkerMaterialSnapshot GetSnapshot()
      {
         using SqlConnection connection = new(_configManager.ConnectionString);
         using SqlCommand command = new("dbo.FALCOM_GetBunkerMaterialSnapshot", connection)
         {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 30
         };

         connection.Open();
         using SqlDataReader reader = command.ExecuteReader();

         var eintraege = new List<BunkerMaterialEintrag>();
         while (reader.Read())
         {
            int arrayIndex = Convert.ToInt32(reader["ArrayIndex"]);
            if (arrayIndex is < 0 or >= 21)
            {
               continue;
            }

            eintraege.Add(new BunkerMaterialEintrag(
               arrayIndex,
               Convert.ToInt32(reader["BuNr"]),
               Convert.ToInt32(reader["MaterialNr"])));
         }

         return new BunkerMaterialSnapshot(eintraege);
      }
   }
}
