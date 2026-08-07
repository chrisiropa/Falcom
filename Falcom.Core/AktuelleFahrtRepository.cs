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
         SetRequiredSqlOptions(connection);
         using SqlDataReader reader = command.ExecuteReader();

         AktuelleFahrtResult result = reader.Read()
            ? ReadAktuelleFahrtResult(reader)
            : AktuelleFahrtResult.Empty("FALCOM_TryCreateNextAktuelleFahrt lieferte kein Ergebnis.");

         return EnrichEvent102Material(result);
      }

      public AktuelleFahrtResult GetAktuelleFahrt()
      {
         using SqlConnection connection = new(_configManager.ConnectionString);
         using SqlCommand command = CreateStoredProcedureCommand(
            connection,
            "dbo.FALCOM_GetAktuelleFahrt");

         connection.Open();
         SetRequiredSqlOptions(connection);
         using SqlDataReader reader = command.ExecuteReader();

         return reader.Read()
            ? ReadAktuelleFahrtResult(reader)
            : AktuelleFahrtResult.Empty("FALCOM_GetAktuelleFahrt lieferte kein Ergebnis.");
      }

      public AktuelleFahrtResult TryClaimSpsResend()
      {
         using SqlConnection connection = new(_configManager.ConnectionString);
         using SqlCommand command = CreateStoredProcedureCommand(
            connection,
            "dbo.FALCOM_TryClaimAktuelleFahrtSpsResend");

         connection.Open();
         SetRequiredSqlOptions(connection);
         using SqlDataReader reader = command.ExecuteReader();

         AktuelleFahrtResult result = reader.Read()
            ? ReadAktuelleFahrtResult(reader)
            : AktuelleFahrtResult.Empty("FALCOM_TryClaimAktuelleFahrtSpsResend lieferte kein Ergebnis.");

         return EnrichEvent102Material(result);
      }

      public AktuelleFahrtResult MarkSpsSendSuccess(
         long? aktuelleFahrtId,
         int? telegrammNummer,
         int? zaehlerAnfahrt)
      {
         using SqlConnection connection = new(_configManager.ConnectionString);
         using SqlCommand command = CreateStoredProcedureCommand(
            connection,
            "dbo.FALCOM_MarkAktuelleFahrtSpsSendSuccess");

         command.Parameters.Add("@AktuelleFahrtID", SqlDbType.BigInt).Value =
            aktuelleFahrtId.HasValue ? aktuelleFahrtId.Value : DBNull.Value;
         command.Parameters.Add("@TelegrammNummer", SqlDbType.Int).Value =
            telegrammNummer.HasValue ? telegrammNummer.Value : DBNull.Value;
         command.Parameters.Add("@ZaehlerAnfahrt", SqlDbType.Int).Value =
            zaehlerAnfahrt.HasValue ? zaehlerAnfahrt.Value : DBNull.Value;

         connection.Open();
         SetRequiredSqlOptions(connection);
         using SqlDataReader reader = command.ExecuteReader();

         return reader.Read()
            ? ReadAktuelleFahrtResult(reader)
            : AktuelleFahrtResult.Empty("FALCOM_MarkAktuelleFahrtSpsSendSuccess lieferte kein Ergebnis.");
      }

      public AktuelleFahrtResult MarkSpsSendFailure(
         long? aktuelleFahrtId,
         string fehler)
      {
         using SqlConnection connection = new(_configManager.ConnectionString);
         using SqlCommand command = CreateStoredProcedureCommand(
            connection,
            "dbo.FALCOM_MarkAktuelleFahrtSpsSendFailure");

         command.Parameters.Add("@AktuelleFahrtID", SqlDbType.BigInt).Value =
            aktuelleFahrtId.HasValue ? aktuelleFahrtId.Value : DBNull.Value;
         command.Parameters.Add("@Fehler", SqlDbType.NVarChar, 1024).Value =
            ToDbValue(fehler);

         connection.Open();
         SetRequiredSqlOptions(connection);
         using SqlDataReader reader = command.ExecuteReader();

         return reader.Read()
            ? ReadAktuelleFahrtResult(reader)
            : AktuelleFahrtResult.Empty("FALCOM_MarkAktuelleFahrtSpsSendFailure lieferte kein Ergebnis.");
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
         SetRequiredSqlOptions(connection);
         using SqlDataReader reader = command.ExecuteReader();

         return reader.Read()
            ? ReadAktuelleFahrtResult(reader)
            : AktuelleFahrtResult.Empty("FALCOM_CompleteAktuelleFahrt lieferte kein Ergebnis.");
      }

      public AktuelleFahrtResult CompleteEinlagerAuftragLkwLeer(
         LkwPlatzLeer207Event lkwPlatzLeerEvent)
      {
         using SqlConnection connection = new(_configManager.ConnectionString);
         using SqlCommand command = CreateStoredProcedureCommand(
            connection,
            "dbo.FALCOM_CompleteEinlagerAuftragLkwLeer");

         command.Parameters.Add("@AuftragsNummer", SqlDbType.BigInt).Value =
            lkwPlatzLeerEvent.AuftragsNummer;
         command.Parameters.Add("@AuftragTeilfahrt", SqlDbType.Int).Value =
            lkwPlatzLeerEvent.TeilfahrtID;
         command.Parameters.Add("@LkwPlatzPositionID", SqlDbType.Int).Value =
            lkwPlatzLeerEvent.LkwPlatzPositionID;
         command.Parameters.Add("@AenderungsZaehler", SqlDbType.Int).Value =
            lkwPlatzLeerEvent.AenderungsZaehler;

         connection.Open();
         SetRequiredSqlOptions(connection);
         using SqlDataReader reader = command.ExecuteReader();

         return reader.Read()
            ? ReadAktuelleFahrtResult(reader)
            : AktuelleFahrtResult.Empty("FALCOM_CompleteEinlagerAuftragLkwLeer lieferte kein Ergebnis.");
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
         SetRequiredSqlOptions(connection);
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

      private AktuelleFahrtResult EnrichEvent102Material(AktuelleFahrtResult result)
      {
         if (!result.Success
             || (result.QuellePositionID is null && result.ZielPositionID is null))
         {
            return result;
         }

         long? materialPositionId = string.Equals(
            result.AuftragsTyp,
            "EINLAGERN",
            StringComparison.OrdinalIgnoreCase)
            ? result.ZielPositionID
            : result.QuellePositionID;

         if (materialPositionId is null)
         {
            return result;
         }

         using SqlConnection connection = new(_configManager.ConnectionString);
         using SqlCommand command = new(
            """
            SELECT TOP (1)
               CONVERT(int, m.ID) AS MaterialNr,
               CONVERT(int, COALESCE(m.diMasseTol_pos, 0)) AS MasseTol_pos,
               CONVERT(int, COALESCE(m.diMasseTol_neg, 0)) AS MasseTol_neg
            FROM dbo.FALCOM_KRAN_POSITION AS p
            LEFT JOIN dbo.FALCOM_LAGER AS l
               ON l.Lagerplatz = CASE
                  WHEN p.PositionsTyp = N'LKW_PLATZ' THEN CONVERT(bigint, p.PositionsNr + 100)
                  ELSE CONVERT(bigint, p.PositionsNr)
               END
            LEFT JOIN dbo.FALCOM_MATERIAL AS m
               ON m.ID = l.MaterialID
            WHERE p.ID = @PositionID;
            """,
            connection);

         command.CommandType = CommandType.Text;
         command.CommandTimeout = 10;
         command.Parameters.Add("@PositionID", SqlDbType.BigInt).Value = materialPositionId.Value;

         connection.Open();
         SetRequiredSqlOptions(connection);

         using SqlDataReader reader = command.ExecuteReader();

         if (!reader.Read())
         {
            return result;
         }

         return result with
         {
            MaterialNr = GetNullableInt32(reader, "MaterialNr"),
            MasseTolPosKg = GetNullableInt32(reader, "MasseTol_pos") ?? 0,
            MasseTolNegKg = GetNullableInt32(reader, "MasseTol_neg") ?? 0
         };
      }

      private static void SetRequiredSqlOptions(SqlConnection connection)
      {
         using SqlCommand command = new(
            """
            SET ANSI_NULLS ON;
            SET ANSI_WARNINGS ON;
            SET ANSI_PADDING ON;
            SET ARITHABORT ON;
            SET CONCAT_NULL_YIELDS_NULL ON;
            SET QUOTED_IDENTIFIER ON;
            SET NUMERIC_ROUNDABORT OFF;
            """,
            connection);

         command.CommandType = CommandType.Text;
         command.CommandTimeout = 10;
         command.ExecuteNonQuery();
      }

      private static AktuelleFahrtResult ReadAktuelleFahrtResult(SqlDataReader reader)
      {
         return new AktuelleFahrtResult(
            GetBoolean(reader, "Created") || GetBoolean(reader, "Completed") || GetBoolean(reader, "IsCurrent"),
            GetString(reader, "Reason"),
            GetNullableInt64(reader, "AktuelleFahrtID"),
            GetNullableInt64(reader, "AuftragID"),
            GetNullableInt32(reader, "AuftragTeilfahrt"),
            GetString(reader, "AuftragsTyp"),
            GetString(reader, "Quelle"),
            GetString(reader, "Ziel"),
            GetNullableInt64(reader, "QuellePositionID"),
            GetNullableInt64(reader, "ZielPositionID"),
            GetNullableInt32(reader, "QuelleUnterposition"),
            GetNullableInt32(reader, "ZielUnterposition"),
            GetNullableDecimal(reader, "SollMengeKg"),
            GetNullableDecimal(reader, "IstMengeKg"),
            GetString(reader, "SpsSendestatus"),
            GetNullableDateTime(reader, "SpsSendewunschAm"),
            GetString(reader, "SpsSendewunschVon"),
            GetString(reader, "SpsSendewunschGrund"),
            GetNullableDateTime(reader, "SpsGesendetAm"),
            GetString(reader, "SpsSendefehler"),
            GetNullableInt32(reader, "SpsLetzteTelegrammNummer"),
            GetNullableInt32(reader, "SpsLetzterZaehlerAnfahrt"),
            GetNullableInt32(reader, "MaterialNr"),
            GetNullableInt32(reader, "MasseTol_pos"),
            GetNullableInt32(reader, "MasseTol_neg"));
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

      private static int? GetNullableInt32(SqlDataReader reader, string name)
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
            : Convert.ToInt32(reader.GetValue(ordinal));
      }

      private static DateTime? GetNullableDateTime(SqlDataReader reader, string name)
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
            : Convert.ToDateTime(reader.GetValue(ordinal));
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
      int? AuftragTeilfahrt,
      string AuftragsTyp,
      string Quelle,
      string Ziel,
      long? QuellePositionID,
      long? ZielPositionID,
      int? QuelleUnterposition,
      int? ZielUnterposition,
      decimal? SollMengeKg,
      decimal? IstMengeKg,
      string SpsSendestatus,
      DateTime? SpsSendewunschAm,
      string SpsSendewunschVon,
      string SpsSendewunschGrund,
      DateTime? SpsGesendetAm,
      string SpsSendefehler,
      int? SpsLetzteTelegrammNummer,
      int? SpsLetzterZaehlerAnfahrt,
      int? MaterialNr,
      int? MasseTolPosKg,
      int? MasseTolNegKg)
   {
      public static AktuelleFahrtResult Empty(string reason)
      {
         return new AktuelleFahrtResult(
            false,
            reason,
            null,
            null,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            null,
            null,
            null,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            null,
            null,
            null,
            null,
            null);
      }
   }
}
