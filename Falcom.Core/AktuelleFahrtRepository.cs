using Microsoft.Data.SqlClient;
using System.Data;

namespace Falcom
{
   public sealed class AktuelleFahrtRepository
   {
      private readonly ConfigManager _configManager;

      public AktuelleFahrtRepository(ConfigManager configManager)
      {
         _configManager = configManager;
      }

      public AktuelleFahrtResult TryCreateNextAktuelleFahrt(long? auftragId)
      {
         using SqlConnection connection = new(_configManager.ConnectionString);
         using SqlCommand command = CreateStoredProcedureCommand(
            connection,
            "dbo.FALCOM_TryCreateNextAktuelleFahrt");

         command.Parameters.Add(
            new SqlParameter("@AuftragID", SqlDbType.BigInt)
            {
               Value = auftragId.HasValue
                  ? auftragId.Value
                  : DBNull.Value
            });

         connection.Open();
         using SqlDataReader reader = command.ExecuteReader();

         return reader.Read()
            ? ReadAktuelleFahrtResult(reader)
            : AktuelleFahrtResult.Empty("FALCOM_TryCreateNextAktuelleFahrt lieferte kein Ergebnis.");
      }

      public AktuelleFahrtResult GetAktuelleFahrt()
      {
         using SqlConnection connection = new(_configManager.ConnectionString);
         using SqlCommand command = CreateStoredProcedureCommand(
            connection,
            "dbo.FALCOM_GetAktuelleFahrt");

         connection.Open();
         using SqlDataReader reader = command.ExecuteReader();

         return reader.Read()
            ? ReadAktuelleFahrtResult(reader)
            : AktuelleFahrtResult.Empty("FALCOM_GetAktuelleFahrt lieferte kein Ergebnis.");
      }

      public AktuelleFahrtResult CompleteAktuelleFahrt(KranfahrtBeendetEvent kranfahrtBeendetEvent)
      {
         using SqlConnection connection = new(_configManager.ConnectionString);
         using SqlCommand command = CreateStoredProcedureCommand(
            connection,
            "dbo.FALCOM_CompleteAktuelleFahrt");

         command.Parameters.Add("@AuftragsNummer", SqlDbType.BigInt).Value =
            kranfahrtBeendetEvent.AuftragsNummer;
         command.Parameters.Add("@AuftragTeilfahrt", SqlDbType.BigInt).Value =
            kranfahrtBeendetEvent.TeilfahrtID;
         command.Parameters.Add("@KranQuelle", SqlDbType.NVarChar, 128).Value =
            ToDbValue(kranfahrtBeendetEvent.KranQuelle);
         command.Parameters.Add("@KranZiel", SqlDbType.NVarChar, 128).Value =
            ToDbValue(kranfahrtBeendetEvent.KranZiel);
         command.Parameters.Add("@IstGewichtKg", SqlDbType.Decimal).Value =
            Convert.ToDecimal(kranfahrtBeendetEvent.IstGewicht);
         command.Parameters["@IstGewichtKg"].Precision = 18;
         command.Parameters["@IstGewichtKg"].Scale = 3;
         command.Parameters.Add("@Status", SqlDbType.Int).Value =
            kranfahrtBeendetEvent.Status;
         command.Parameters.Add("@AenderungsZaehler", SqlDbType.Int).Value =
            kranfahrtBeendetEvent.ÄnderungsZähler;

         connection.Open();
         using SqlDataReader reader = command.ExecuteReader();

         return reader.Read()
            ? ReadAktuelleFahrtResult(reader)
            : AktuelleFahrtResult.Empty("FALCOM_CompleteAktuelleFahrt lieferte kein Ergebnis.");
      }
      public AktuelleFahrtResult FailAktuelleFahrt(
         long? aktuelleFahrtId,
         string bemerkung)
      {
         using SqlConnection connection = new(_configManager.ConnectionString);
         using SqlCommand command = CreateStoredProcedureCommand(
            connection,
            "dbo.FALCOM_FailAktuelleFahrt");

         command.Parameters.Add(
            new SqlParameter("@AktuelleFahrtID", SqlDbType.BigInt)
            {
               Value = aktuelleFahrtId.HasValue
                  ? aktuelleFahrtId.Value
                  : DBNull.Value
            });

         command.Parameters.Add("@Bemerkung", SqlDbType.NVarChar, 1024).Value =
            ToDbValue(bemerkung);

         connection.Open();
         using SqlDataReader reader = command.ExecuteReader();

         return reader.Read()
            ? ReadAktuelleFahrtResult(reader)
            : AktuelleFahrtResult.Empty("FALCOM_FailAktuelleFahrt lieferte kein Ergebnis.");
      }

      private static SqlCommand CreateStoredProcedureCommand(
         SqlConnection connection,
         string procedureName)
      {
         return new SqlCommand(procedureName, connection)
         {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 30
         };
      }

      private static AktuelleFahrtResult ReadAktuelleFahrtResult(SqlDataReader reader)
      {
         return new AktuelleFahrtResult(
            GetBoolean(reader, "Created") || GetBoolean(reader, "Completed") || GetBoolean(reader, "IsCurrent"),
            GetString(reader, "Reason"),
            GetNullableInt64(reader, "AktuelleFahrtID"),
            GetNullableInt64(reader, "AuftragID"),
            GetString(reader, "AuftragsTyp"),
            GetString(reader, "Quelle"),
            GetString(reader, "Ziel"),
            GetNullableInt64(reader, "QuellePositionID"),
            GetNullableInt64(reader, "ZielPositionID"),
            GetNullableDecimal(reader, "SollMengeKg"),
            GetNullableDecimal(reader, "IstMengeKg"));
      }

      private static object ToDbValue(string? value)
      {
         return string.IsNullOrWhiteSpace(value)
            ? DBNull.Value
            : value.Trim();
      }

      private static bool GetBoolean(SqlDataReader reader, string name)
      {
         int ordinal;

         try
         {
            ordinal = reader.GetOrdinal(name);
         }
         catch (IndexOutOfRangeException)
         {
            return false;
         }

         return reader.IsDBNull(ordinal)
            ? false
            : Convert.ToBoolean(reader.GetValue(ordinal));
      }

      private static string GetString(SqlDataReader reader, string name)
      {
         int ordinal;

         try
         {
            ordinal = reader.GetOrdinal(name);
         }
         catch (IndexOutOfRangeException)
         {
            return string.Empty;
         }

         return reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToString(reader.GetValue(ordinal)) ?? string.Empty;
      }

      private static long? GetNullableInt64(SqlDataReader reader, string name)
      {
         int ordinal;

         try
         {
            ordinal = reader.GetOrdinal(name);
         }
         catch (IndexOutOfRangeException)
         {
            return null;
         }

         return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToInt64(reader.GetValue(ordinal));
      }

      private static decimal? GetNullableDecimal(SqlDataReader reader, string name)
      {
         int ordinal;

         try
         {
            ordinal = reader.GetOrdinal(name);
         }
         catch (IndexOutOfRangeException)
         {
            return null;
         }

         return reader.IsDBNull(ordinal)
            ? null
            : Convert.ToDecimal(reader.GetValue(ordinal));
      }
   }

   public sealed record AktuelleFahrtResult(
      bool Success,
      string Reason,
      long? AktuelleFahrtID,
      long? AuftragID,
      string AuftragsTyp,
      string Quelle,
      string Ziel,
      long? QuellePositionID,
      long? ZielPositionID,
      decimal? SollMengeKg,
      decimal? IstMengeKg)
   {
      public static AktuelleFahrtResult Empty(string reason)
      {
         return new AktuelleFahrtResult(
            false,
            reason,
            null,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null);
      }
   }
}
