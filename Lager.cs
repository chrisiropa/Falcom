using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falcom
{
   public class Lagerplatz
   {
      // Private Felder
      private int lagerplatzNr;
      private bool active;
      private int restmenge;
      private float cu;
      private float mn;
      private float c;
      private float si;
      private float s;
      private float mg;
      private string bezeichnung;

      public Lagerplatz()
      {
         // Standardkonstruktor
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
		private readonly Logging _logger;
		private readonly Dictionary<int, Lagerplatz> lagerplätze;

		public Lager(ConfigManager configManager, Logging logger)
		{
			_configManager = configManager;
			_logger = logger;
         lagerplätze = new Dictionary<int, Lagerplatz>();

         try
         {
            SimpleSqlQuery query = new SimpleSqlQuery(_configManager.ConnectionString , "select * from FALCOM_Lager order by Lagerplatz");
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

                     string bezeichnung = Convert.ToString(row["Bezeichnung"]);

                     // Erstelle einen Lagerplatz und füge ihn dem Dictionary hinzu
                     Lagerplatz platz = new Lagerplatz(lagerplatz, active, restmenge, cu, mn, c, si, s, mg, bezeichnung);
                     lagerplätze.Add(lagerplatz, platz);
                  }
                  catch(Exception e)
                  {
                     _logger.ZLog(ELF.ERROR, "Tabelle FALCOM_Lager -> {0}", e.Message);
                  }
               }
            }
         }
         catch (Exception e)
         {
            _logger.ZLog(ELF.ERROR, "Tabelle FALCOM_Lager (2) -> {0}", e.Message);
         }
		}

      public float[] GetCuValues()
      {
         // Liste für die Kupfer-Werte (Cu)
         List<float> cuWerte = new List<float>();

         // Durchlaufe alle Lagerplatz-Objekte und sammle die Cu-Werte
         foreach (var lagerplatz in lagerplätze.Values)
         {
            if(lagerplatz.LagerplatzNr > 16)
            {
               continue;
            }
            cuWerte.Add(lagerplatz.Cu);
         }

         // Konvertiere die Liste in ein Array vom Typ double und gebe es zurück
         return cuWerte.ToArray();
      }

      // Funktion, die die Mn-Werte als double[] zurückgibt
      public float[] GetMnValues()
      {
         // Liste für die Mangan-Werte (Mn)
         List<float> mnWerte = new List<float>();

         // Durchlaufe alle Lagerplatz-Objekte und sammle die Mn-Werte
         foreach (var lagerplatz in lagerplätze.Values)
         {
            if(lagerplatz.LagerplatzNr > 16)
            {
               continue;
            }
            mnWerte.Add(lagerplatz.Mn);
         }

         // Konvertiere die Liste in ein Array vom Typ double und gebe es zurück
         return mnWerte.ToArray();
      }
	}
}
