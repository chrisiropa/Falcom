using System;
using System.Collections.Generic;

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

      public Lagerplatz()
      {
      }

      public Lagerplatz(int lagerplatzNr, bool active, int restmenge, float cu, float mn, float c, float si, float s, float mg, string bezeichnung)
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
                $"Bezeichnung: {bezeichnung}";
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
   }

   public class Lager
   {
      private readonly ConfigManager _configManager;
      private readonly ILogger<Lager> _logger;
      private readonly Dictionary<int, Lagerplatz> lagerplätze;

      public Lager(ConfigManager configManager, ILogger<Lager> logger)
      {
         _configManager = configManager;
         _logger = logger;
         lagerplätze = new Dictionary<int, Lagerplatz>();

         try
         {
            SimpleSqlQuery query = new SimpleSqlQuery(_configManager.ConnectionString, "select * from FALCOM_Lager order by Lagerplatz");
            if (query.QueryResult != null)
            {
               foreach (Dictionary<string, object> row in query.QueryResult)
               {
                  try
                  {
                     int lagerplatz = Convert.ToInt32(row["Lagerplatz"]);
                     bool active = Convert.ToBoolean(row["Aktiv"]);
                     int restmenge = Convert.ToInt32(row["Restmenge"]);
                     float cu = (float)Math.Round(Convert.ToSingle(row["Cu"]), 3);
                     float mn = (float)Math.Round(Convert.ToSingle(row["Mn"]), 3);
                     float c = (float)Math.Round(Convert.ToSingle(row["C"]), 3);
                     float si = (float)Math.Round(Convert.ToSingle(row["Si"]), 3);
                     float s = (float)Math.Round(Convert.ToSingle(row["S"]), 3);
                     float mg = (float)Math.Round(Convert.ToSingle(row["Mg"]), 3);

                     string bezeichnung = Convert.ToString(row["Bezeichnung"]) ?? string.Empty;

                     Lagerplatz platz = new(lagerplatz, active, restmenge, cu, mn, c, si, s, mg, bezeichnung);
                     lagerplätze.Add(lagerplatz, platz);
                  }
                  catch (Exception e)
                  {
                     _logger.LogError(e, "Tabelle FALCOM_Lager konnte nicht gelesen werden.");
                  }
               }
            }
         }
         catch (Exception e)
         {
            _logger.LogError(e, "Tabelle FALCOM_Lager konnte nicht geladen werden.");
         }
      }

      public float[] GetCuValues()
      {
         List<float> cuWerte = new List<float>();

         foreach (var lagerplatz in lagerplätze.Values)
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

         foreach (var lagerplatz in lagerplätze.Values)
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
