using Microsoft.Data.SqlClient;
using System.Data;

namespace Falcom
{
   public sealed class KranPositionenRepository
   {
      private readonly ConfigManager _configManager;

      public KranPositionenRepository(ConfigManager configManager)
      {
         _configManager = configManager;
      }

      public KranPositionenSnapshot GetSnapshot()
      {
         using SqlConnection connection = new(_configManager.ConnectionString);
         using SqlCommand command = new("dbo.FALCOM_GetKranPositionenSnapshot", connection)
         {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 30
         };

         connection.Open();
         using SqlDataReader reader = command.ExecuteReader();

         var builderByArrayIndex = new SortedDictionary<int, KranPositionenEintragBuilder>();
         while (reader.Read())
         {
            int arrayIndex = Convert.ToInt32(reader["ArrayIndex"]);
            if (arrayIndex < 0 || arrayIndex > 50)
            {
               continue;
            }

            if (!builderByArrayIndex.TryGetValue(arrayIndex, out KranPositionenEintragBuilder? builder))
            {
               builder = new KranPositionenEintragBuilder(
                  arrayIndex,
                  GetString(reader, "stArt"),
                  GetString(reader, "stBezeichnung"),
                  GetString(reader, "stPositionsTyp"),
                  Convert.ToInt32(reader["iID"]),
                  Convert.ToInt32(reader["diKatze_Start_X"]),
                  Convert.ToInt32(reader["diKatze_Breite_X"]),
                  Convert.ToInt32(reader["diKran_Start_Y"]),
                  Convert.ToInt32(reader["diKran_Laenge_Y"]),
                  Convert.ToInt32(reader["diHub_Start_Z"]),
                  Convert.ToInt32(reader["diHub_Hoehe_Z"]),
                  Convert.ToInt32(reader["iPositionsAnz"]));
               builderByArrayIndex.Add(arrayIndex, builder);
            }

            if (reader["PositionArrayIndex"] != DBNull.Value)
            {
               int positionArrayIndex = Convert.ToInt32(reader["PositionArrayIndex"]);
               if (positionArrayIndex >= 0 && positionArrayIndex <= 9)
               {
                  builder.Positionen.Add(new KranPositionenUnterposition(
                     positionArrayIndex,
                     Convert.ToInt32(reader["diKatze_X"]),
                     Convert.ToInt32(reader["diKran_Y"])));
               }
            }
         }

         var eintraege = builderByArrayIndex.Values
            .Select(builder => builder.ToEintrag())
            .ToList();

         return new KranPositionenSnapshot(eintraege);
      }

      private static string GetString(SqlDataReader reader, string name)
      {
         return reader[name] == DBNull.Value ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;
      }

      private sealed class KranPositionenEintragBuilder
      {
         public KranPositionenEintragBuilder(
            int arrayIndex,
            string art,
            string bezeichnung,
            string positionsTyp,
            int id,
            int diKatzeStartX,
            int diKatzeBreiteX,
            int diKranStartY,
            int diKranLaengeY,
            int diHubStartZ,
            int diHubHoeheZ,
            int positionsAnz)
         {
            ArrayIndex = arrayIndex;
            Art = art;
            Bezeichnung = bezeichnung;
            PositionsTyp = positionsTyp;
            ID = id;
            DiKatzeStartX = diKatzeStartX;
            DiKatzeBreiteX = diKatzeBreiteX;
            DiKranStartY = diKranStartY;
            DiKranLaengeY = diKranLaengeY;
            DiHubStartZ = diHubStartZ;
            DiHubHoeheZ = diHubHoeheZ;
            PositionsAnz = positionsAnz;
         }

         public int ArrayIndex { get; }
         public string Art { get; }
         public string Bezeichnung { get; }
         public string PositionsTyp { get; }
         public int ID { get; }
         public int DiKatzeStartX { get; }
         public int DiKatzeBreiteX { get; }
         public int DiKranStartY { get; }
         public int DiKranLaengeY { get; }
         public int DiHubStartZ { get; }
         public int DiHubHoeheZ { get; }
         public int PositionsAnz { get; }
         public List<KranPositionenUnterposition> Positionen { get; } = new();

         public KranPositionenEintrag ToEintrag()
         {
            return new KranPositionenEintrag(
               ArrayIndex,
               Art,
               Bezeichnung,
               PositionsTyp,
               ID,
               DiKatzeStartX,
               DiKatzeBreiteX,
               DiKranStartY,
               DiKranLaengeY,
               DiHubStartZ,
               DiHubHoeheZ,
               PositionsAnz,
               Positionen.OrderBy(position => position.ArrayIndex).ToList());
         }
      }
   }
}
