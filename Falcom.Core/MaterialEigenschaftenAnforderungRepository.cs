using Microsoft.Data.SqlClient;
using System.Data;

namespace Falcom;

public sealed class MaterialEigenschaftenAnforderungRepository
{
   private readonly ConfigManager _configManager;

   public MaterialEigenschaftenAnforderungRepository(ConfigManager configManager)
   {
      _configManager = configManager;
   }

   public MaterialEigenschaftenDbAnforderung? TryClaim()
   {
      using SqlConnection connection = new(_configManager.ConnectionString);
      using SqlCommand command = new("dbo.FALCOM_TryClaimMaterialEigenschaftenEvent105", connection)
      {
         CommandType = CommandType.StoredProcedure,
         CommandTimeout = 30
      };

      connection.Open();
      using SqlDataReader reader = command.ExecuteReader();
      if (!reader.Read())
      {
         return null;
      }

      return new MaterialEigenschaftenDbAnforderung(
         Convert.ToInt32(reader["ID"]),
         Convert.ToDateTime(reader["AngefordertAm"]),
         Convert.ToString(reader["AngefordertVon"]) ?? string.Empty,
         Convert.ToString(reader["Vorgang"]) ?? string.Empty,
         reader["Grund"] == DBNull.Value ? null : Convert.ToString(reader["Grund"]),
         Convert.ToInt32(reader["RetryCount"]));
   }

   public void Complete(int anforderungID, bool erfolgreich, string? fehler)
   {
      using SqlConnection connection = new(_configManager.ConnectionString);
      using SqlCommand command = new("dbo.FALCOM_CompleteMaterialEigenschaftenEvent105", connection)
      {
         CommandType = CommandType.StoredProcedure,
         CommandTimeout = 30
      };

      command.Parameters.Add("@AnforderungID", SqlDbType.Int).Value = anforderungID;
      command.Parameters.Add("@Erfolgreich", SqlDbType.Bit).Value = erfolgreich;
      command.Parameters.Add("@Fehler", SqlDbType.NVarChar, 1000).Value =
         string.IsNullOrWhiteSpace(fehler) ? DBNull.Value : fehler;

      connection.Open();
      command.ExecuteNonQuery();
   }
}

public sealed record MaterialEigenschaftenDbAnforderung(
   int ID,
   DateTime AngefordertAm,
   string AngefordertVon,
   string Vorgang,
   string? Grund,
   int RetryCount);
