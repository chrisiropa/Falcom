using Microsoft.Data.SqlClient;
using System.Data;

namespace Falcom
{
   public sealed class MaterialEigenschaftenRepository
   {
      private readonly ConfigManager _configManager;

      public MaterialEigenschaftenRepository(ConfigManager configManager)
      {
         _configManager = configManager;
      }

      public MaterialEigenschaftenSnapshot GetSnapshot()
      {
         using SqlConnection connection = new(_configManager.ConnectionString);
         using SqlCommand command = new("dbo.FALCOM_GetMaterialEigenschaftenSnapshot", connection)
         {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 30
         };

         connection.Open();
         using SqlDataReader reader = command.ExecuteReader();

         var eintraege = new List<MaterialEigenschaftenEintrag>();
         while (reader.Read())
         {
            int arrayIndex = Convert.ToInt32(reader["ArrayIndex"]);
            if (arrayIndex is < 0 or > 20)
            {
               continue;
            }

            eintraege.Add(new MaterialEigenschaftenEintrag(
               arrayIndex,
               Convert.ToInt32(reader["iID"]),
               GetString(reader, "Mat_Name"),
               GetNullableDateTime(reader, "Datum_Zeit"),
               Convert.ToInt32(reader["diMasseGattMin"]),
               Convert.ToInt32(reader["diZeitAbtippen"]),
               Convert.ToInt32(reader["diMasseVorAbtippen"]),
               Convert.ToInt32(reader["diMasseTol_pos"]),
               Convert.ToInt32(reader["diMasseTol_neg"]),
               Convert.ToSingle(reader["rKraftStufenPro100Kg"]),
               Convert.ToBoolean(reader["xAbwurfFlach"]),
               Convert.ToBoolean(reader["xAbwurfAbzett"]),
               Convert.ToBoolean(reader["xAbwurfTippen"]),
               Convert.ToBoolean(reader["xNachfassenMag_Aus"]),
               Convert.ToBoolean(reader["xNachfassenMag_Dauernd"]),
               Convert.ToBoolean(reader["xKreislauf"])));
         }

         return new MaterialEigenschaftenSnapshot(eintraege);
      }

      public int SaveSnapshotFromSps(MaterialEigenschaftenSnapshot snapshot)
      {
         using SqlConnection connection = new(_configManager.ConnectionString);
         connection.Open();
         using SqlTransaction transaction = connection.BeginTransaction();

         try
         {
            int saved = 0;
            foreach (MaterialEigenschaftenEintrag eintrag in snapshot.Eintraege
                        .Where(item => item.ArrayIndex > 0 && item.ID > 0 && item.ID <= 20))
            {
               using SqlCommand existsCommand = new(
                  "SELECT CONVERT(bit, CASE WHEN EXISTS (SELECT 1 FROM dbo.FALCOM_MATERIAL WHERE ID = @ID) THEN 1 ELSE 0 END);",
                  connection,
                  transaction);
               existsCommand.Parameters.Add("@ID", SqlDbType.BigInt).Value = eintrag.ID;
               bool exists = Convert.ToBoolean(existsCommand.ExecuteScalar());

               if (exists)
               {
                  using SqlCommand updateCommand = new(
                     """
                     UPDATE dbo.FALCOM_MATERIAL
                        SET MaterialName = @MaterialName,
                            Datum_Zeit = @DatumZeit,
                            diMasseGattMin = @DiMasseGattMin,
                            diZeitAbtippen = @DiZeitAbtippen,
                            diMasseVorAbtippen = @DiMasseVorAbtippen,
                            diMasseTol_pos = @DiMasseTolPos,
                            diMasseTol_neg = @DiMasseTolNeg,
                            rKraftStufenPro100Kg = @RKraftStufenPro100Kg,
                            xAbwurfFlach = @XAbwurfFlach,
                            xAbwurfAbzett = @XAbwurfAbzett,
                            xAbwurfTippen = @XAbwurfTippen,
                            xNachfassenMag_Aus = @XNachfassenMagAus,
                            xNachfassenMag_Dauernd = @XNachfassenMagDauernd,
                            xKreislauf = @XKreislauf
                      WHERE ID = @ID;
                     """,
                     connection,
                     transaction);
                  AddSaveParameters(updateCommand, eintrag);
                  saved += updateCommand.ExecuteNonQuery();
                  continue;
               }

               using SqlCommand identityOnCommand = new("SET IDENTITY_INSERT dbo.FALCOM_MATERIAL ON;", connection, transaction);
               identityOnCommand.ExecuteNonQuery();

               using SqlCommand insertCommand = new(
                  """
                  INSERT dbo.FALCOM_MATERIAL
                     (ID, MaterialName, Cu, Mn, C, Si, Cr, Mg,
                      Cu_Min, Cu_Max, Mn_Min, Mn_Max, Bemerkung,
                      Ziel_Cu_Min, Ziel_Cu_Max, Ziel_Mn_Min, Ziel_Mn_Max,
                      Ziel_C_Min, Ziel_C_Max, Ziel_Si_Min, Ziel_Si_Max,
                      Ziel_Cr_Min, Ziel_Cr_Max, Ziel_Mg_Min, Ziel_Mg_Max,
                      Datum_Zeit, diMasseGattMin, diZeitAbtippen, diMasseVorAbtippen,
                      diMasseTol_pos, diMasseTol_neg, rKraftStufenPro100Kg,
                      xAbwurfFlach, xAbwurfAbzett, xAbwurfTippen,
                      xNachfassenMag_Aus, xNachfassenMag_Dauernd, xKreislauf)
                  VALUES
                     (@ID, @MaterialName, 0, 0, 0, 0, 0, 0,
                      0, 0, 0, 0, NULL,
                      0, 999, 0, 999,
                      0, 999, 0, 999,
                      0, 999, 0, 999,
                      @DatumZeit, @DiMasseGattMin, @DiZeitAbtippen, @DiMasseVorAbtippen,
                      @DiMasseTolPos, @DiMasseTolNeg, @RKraftStufenPro100Kg,
                      @XAbwurfFlach, @XAbwurfAbzett, @XAbwurfTippen,
                      @XNachfassenMagAus, @XNachfassenMagDauernd, @XKreislauf);
                  """,
                  connection,
                  transaction);
               AddSaveParameters(insertCommand, eintrag);
               saved += insertCommand.ExecuteNonQuery();

               using SqlCommand identityOffCommand = new("SET IDENTITY_INSERT dbo.FALCOM_MATERIAL OFF;", connection, transaction);
               identityOffCommand.ExecuteNonQuery();
            }

            transaction.Commit();
            return saved;
         }
         catch
         {
            transaction.Rollback();
            throw;
         }
      }

      private static string GetString(SqlDataReader reader, string name)
      {
         return reader[name] == DBNull.Value ? string.Empty : Convert.ToString(reader[name]) ?? string.Empty;
      }

      private static DateTime? GetNullableDateTime(SqlDataReader reader, string name)
      {
         return reader[name] == DBNull.Value ? null : Convert.ToDateTime(reader[name]);
      }

      private static void AddSaveParameters(SqlCommand command, MaterialEigenschaftenEintrag eintrag)
      {
         command.Parameters.Add("@ID", SqlDbType.BigInt).Value = eintrag.ID;
         command.Parameters.Add("@MaterialName", SqlDbType.NVarChar, 128).Value =
            string.IsNullOrWhiteSpace(eintrag.MatName) ? $"Material {eintrag.ID}" : eintrag.MatName.Trim();
         command.Parameters.Add("@DatumZeit", SqlDbType.DateTime2).Value =
            eintrag.DatumZeit is null ? DBNull.Value : eintrag.DatumZeit.Value;
         command.Parameters.Add("@DiMasseGattMin", SqlDbType.Int).Value = eintrag.DiMasseGattMin;
         command.Parameters.Add("@DiZeitAbtippen", SqlDbType.Int).Value = eintrag.DiZeitAbtippen;
         command.Parameters.Add("@DiMasseVorAbtippen", SqlDbType.Int).Value = eintrag.DiMasseVorAbtippen;
         command.Parameters.Add("@DiMasseTolPos", SqlDbType.Int).Value = eintrag.DiMasseTolPos;
         command.Parameters.Add("@DiMasseTolNeg", SqlDbType.Int).Value = eintrag.DiMasseTolNeg;
         command.Parameters.Add("@RKraftStufenPro100Kg", SqlDbType.Decimal).Value = Convert.ToDecimal(eintrag.RKraftStufenPro100Kg);
         command.Parameters.Add("@XAbwurfFlach", SqlDbType.Bit).Value = eintrag.XAbwurfFlach;
         command.Parameters.Add("@XAbwurfAbzett", SqlDbType.Bit).Value = eintrag.XAbwurfAbzett;
         command.Parameters.Add("@XAbwurfTippen", SqlDbType.Bit).Value = eintrag.XAbwurfTippen;
         command.Parameters.Add("@XNachfassenMagAus", SqlDbType.Bit).Value = eintrag.XNachfassenMagAus;
         command.Parameters.Add("@XNachfassenMagDauernd", SqlDbType.Bit).Value = eintrag.XNachfassenMagDauernd;
         command.Parameters.Add("@XKreislauf", SqlDbType.Bit).Value = eintrag.XKreislauf;
      }
   }
}
