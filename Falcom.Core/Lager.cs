using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Falcom
{
   public class Lagerplatz
   {
      private int lagerplatzNr;
      private bool active;
      private int restmenge;
      private float cu;
      private float mn;
      private float c;
      private float si;
      private float s;
      private float mg;
      private string bezeichnung = string.Empty;
      private string schrottsorte = string.Empty;
      private float toleranz;

      public Lagerplatz()
      {
      }

      public Lagerplatz(
         int lagerplatzNr,
         bool active,
         int restmenge,
         float cu,
         float mn,
         float c,
         float si,
         float s,
         float mg,
         string bezeichnung,
         string schrottsorte,
         float toleranz)
      {
         this.lagerplatzNr = lagerplatzNr;
         this.active = active;
         this.restmenge = restmenge;
         this.cu = cu;
         this.mn = mn;
         this.c = c;
         this.si = si;
         this.s = s;
         this.mg = mg;
         this.bezeichnung = bezeichnung;
         this.schrottsorte = schrottsorte;
         this.toleranz = toleranz;
      }

      public override string ToString()
      {
         return $"Lagerplatz: {lagerplatzNr}\n" +
                $"Aktiv: {active}\n" +
                $"Restmenge: {restmenge}\n" +
                $"Cu: {cu}\n" +
                $"Mn: {mn}\n" +
                $"C: {c}\n" +
                $"Si: {si}\n" +
                $"S: {s}\n" +
                $"Mg: {mg}\n" +
                $"Bezeichnung: {bezeichnung}\n" +
                $"Schrottsorte: {schrottsorte}\n" +
                $"Toleranz: {toleranz} kg";
      }

      public int LagerplatzNr
      {
         get { return lagerplatzNr; }
         set { lagerplatzNr = value; }
      }

      public bool Active
      {
         get { return active; }
         set { active = value; }
      }

      public int Restmenge
      {
         get { return restmenge; }
         set { restmenge = value; }
      }

      public float Cu
      {
         get { return cu; }
         set { cu = value; }
      }

      public float Mn
      {
         get { return mn; }
         set { mn = value; }
      }

      public float C
      {
         get { return c; }
         set { c = value; }
      }

      public float Si
      {
         get { return si; }
         set { si = value; }
      }

      public float S
      {
         get { return s; }
         set { s = value; }
      }

      public float Mg
      {
         get { return mg; }
         set { mg = value; }
      }

      public string Bezeichnung
      {
         get { return bezeichnung; }
         set { bezeichnung = value; }
      }

      public string Schrottsorte
      {
         get { return schrottsorte; }
         set { schrottsorte = value; }
      }

      public float Toleranz
      {
         get { return toleranz; }
         set { toleranz = value; }
      }
   }

   public class Lager
   {
      private readonly ConfigManager _configManager;
      private readonly ILogger<Lager> _logger;
      private readonly Dictionary<int, Lagerplatz> lagerplaetze;

      public Lager(ConfigManager configManager, ILogger<Lager> logger)
      {
         _configManager = configManager;
         _logger = logger;
         lagerplaetze = new Dictionary<int, Lagerplatz>();

         try
         {
            LoadLagerplaetze();
         }
         catch (Exception e)
         {
            _logger.LogError(e, "000E|Tabelle FALCOM_Lager konnte nicht geladen werden.");
         }
      }

      private void LoadLagerplaetze()
      {
         using var connection = new SqlConnection(_configManager.ConnectionString);
         using var command = new SqlCommand("dbo.FALCOM_GetLagerplaetze", connection)
         {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 30
         };

         connection.Open();
         using SqlDataReader reader = command.ExecuteReader();
         while (reader.Read())
         {
            try
            {
               int lagerplatz = Convert.ToInt32(reader["Lagerplatz"]);
               bool active = Convert.ToBoolean(reader["Aktiv"]);
               int restmenge = Convert.ToInt32(reader["Restmenge"]);
               float cu = (float)Math.Round(Convert.ToSingle(reader["Cu"]), 3);
               float mn = (float)Math.Round(Convert.ToSingle(reader["Mn"]), 3);
               float c = (float)Math.Round(Convert.ToSingle(reader["C"]), 3);
               float si = (float)Math.Round(Convert.ToSingle(reader["Si"]), 3);
               float s = (float)Math.Round(Convert.ToSingle(reader["S"]), 3);
               float mg = (float)Math.Round(Convert.ToSingle(reader["Mg"]), 3);

               string bezeichnung = Convert.ToString(reader["Bezeichnung"]) ?? string.Empty;
               string schrottsorte = Convert.ToString(reader["Schrottsorte"]) ?? "Unbekannt";
               float toleranz = (float)Math.Round(Convert.ToSingle(reader["Toleranz"]), 3);

               Lagerplatz platz = new(
                  lagerplatz,
                  active,
                  restmenge,
                  cu,
                  mn,
                  c,
                  si,
                  s,
                  mg,
                  bezeichnung,
                  schrottsorte,
                  toleranz);

               lagerplaetze[lagerplatz] = platz;
            }
            catch (Exception e)
            {
               _logger.LogError(e, "000D|Datensatz aus FALCOM_GetLagerplaetze konnte nicht verarbeitet werden.");
            }
         }
      }

      public float[] GetCuValues()
      {
         List<float> cuWerte = new List<float>();

         foreach (var lagerplatz in lagerplaetze.Values)
         {
            if (lagerplatz.LagerplatzNr > 16)
            {
               continue;
            }

            cuWerte.Add(lagerplatz.Cu);
         }

         return cuWerte.ToArray();
      }

      public float[] GetMnValues()
      {
         List<float> mnWerte = new List<float>();

         foreach (var lagerplatz in lagerplaetze.Values)
         {
            if (lagerplatz.LagerplatzNr > 16)
            {
               continue;
            }

            mnWerte.Add(lagerplatz.Mn);
         }

         return mnWerte.ToArray();
      }
   }
}
