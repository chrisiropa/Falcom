using System;
using System.IO;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace Falcom
{
   public class Appsettings
   {
      // Die Namen der Eigenschaften müssen exakt mit den Schlüsseln in der appsettings.json übereinstimmen
      public string Server { get; set; } = string.Empty;
      public string DatabaseName { get; set; } = string.Empty;
      public string User { get; set; } = string.Empty;
      public string Password { get; set; } = string.Empty;

      public string LogfilePath { get; set; } = string.Empty;

      public string KranLiveHubUrl { get; set; } = string.Empty;
   }

   public class ConfigManager
   {
      private readonly string connectionString = "";
      private readonly string logfilePath = "";
      private readonly string kranLiveHubUrl = "";
      private readonly string executionDirectory = "";
      public static string TraegerLicenseKey = "AALOERR5OO7EKFNQCABINGCH6TYOVHPLFC2QCUEAXLKSL6ZHZ5D3MX4J7SHGOG2SD22JBJWGHUWWXQ5HKWI7OFVYYMERDPQDC7ZW7ZPKELPHGJAYUBOJT3YR4OXOUKS2YBBPBRGK6SBRI4DBXF4NGVKZUATMW3VI7EALG5FQN2FTYIDJGXOHOCH2GPOXNQO5B4GPJTKWZNGUEQULQX56E6NCBRCDX35VJBWLQDD4QANXIWUKO7D3Q7SWDDL55ZZCSN7NLHKB3W5O524VIXFPLVJIYKM5UYKPL7Q2LKVZDVTVG3B3COHS4I335TZ6NT5H2LTGGEGEUHCO3MCPZB2KOAFRJP5KQ2KS4P5GG7RSE4KKCVTOYNJ3T3UBHZXFI6B7AZLZCDKXGIP4CHZXZGN7H4LDBS2LPWW4D6MSDTQBAGEN5RVMQMSUNORR4IR6CYYEVQA6X6DE5T65456OVQCUAUXTHXMCPAIS2CTVURNL3BYF2RWCV4OVYUYBM2NLTXT5J2BYOTUM6E2IOTQH64WTVBVSRGOIYLIQQCXGJB27SZEL6ZF4C4TUERFOHCV7M2MHFOD6ICK2YI2P2ZPYUEJN6FO47URN4N76Z7LNRN5GECCNADIF4IPWI3BMT4BG52YS7FQGZN7OBIJRMFQ5WE7UY37E6R7HK4635RRFWD6COF7R24GNCCF67PI3EQBLUJCD5XI54RAHIEHXEACSFX52XO7EUIWUFDBZIDJZGSHZ7B76R66BAO2VBKNSUNY5FRNNVUBBWT5V2YYBLUUC7YLMJBWAVNCI3QAQ2VO6TZ4DGPNBZ7R7R2FU6WLUEVLXD4YHIYV5ZYXNWKDLHKHIIEEWBUBGXTSEEO4Z7EWPK6ILGHETAWNY56WZBSWIO4L5SOQ2UWMO44P7VWSGFF6LDGNJ2SYYIXAEQT3HM4CJ3ZCRG4U3MAVSODOAWOLO5F24VNUQ6DLQIH44PLQLTN3S77UWDLJRJUNIHBM3AZDBOH5FTAEG5X2SRW6LBKYZSCNXFX7YNW2QEXSDGUJYPLURNIPOVVQVFMJCOMGJJP7TDXYGMXKBSXOHUHY6OGCV3TWSFBP6USJBUZUUVKVKVIR77P2O7ZMH5YLKXALGUWE5ORIOUWBIZCUIIJN3RO55VLDXVHTHN6ZGRSANELKEFUBA3JH4THRV2NNYHTIKROC7NTZVCBXNWM3ANEUZA264NP3NBOA2BJBUYH3R5AVX6ZB5ODODE6XIBN4BMQHDXKN3VKPMFXZDKSD7I7M5P";

      
      public string ConnectionString => connectionString;

      public string LogfilePath => logfilePath;

      public string KranLiveHubUrl => kranLiveHubUrl;
      
      public ConfigManager(IOptions<Appsettings> appSettings)
      {
         try
         {
            var settings = appSettings.Value;
            logfilePath = settings.LogfilePath;

            AssemblyInfoWrapper iw = new();

            // Path.GetDirectoryName kann null zurückgeben. Wenn es null ist, 
            // wird der Standardwert (string.Empty) zugewiesen.
            executionDirectory = Path.GetDirectoryName(iw.ExecutionPath) ?? string.Empty;


            SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();

            csb.DataSource = settings.Server;
            csb.InitialCatalog = settings.DatabaseName;
            csb.UserID = settings.User;
            csb.Password = settings.Password;
            csb.Encrypt = false;


            connectionString = csb.ConnectionString;
            kranLiveHubUrl = LoadKranLiveHubUrlFromDatabase(settings.KranLiveHubUrl);

         }
         catch (Exception e)
         {
            Console.WriteLine("ConfigManger.ConfigManager -> {0}", e.Message);
         }
      }
      private string LoadKranLiveHubUrlFromDatabase(string fallback)
      {
         try
         {
            string scheme = LoadParameterValue("KranLiveHubScheme", "https");
            string host = LoadParameterValue("KranLiveHubHost", "localhost");
            string port = LoadParameterValue("KundenVisuModernHttpsPort", string.Empty);
            string path = LoadParameterValue("KranLiveHubPath", "/falcom-kran-hub");

            if (string.IsNullOrWhiteSpace(host)
                || string.IsNullOrWhiteSpace(port)
                || !int.TryParse(port, out int portNumber)
                || portNumber is < 1 or > 65535)
            {
               return fallback;
            }

            if (string.IsNullOrWhiteSpace(scheme))
            {
               scheme = "https";
            }

            if (string.IsNullOrWhiteSpace(path))
            {
               path = "/falcom-kran-hub";
            }

            if (!path.StartsWith("/", StringComparison.Ordinal))
            {
               path = "/" + path;
            }

            return $"{scheme.Trim()}://{host.Trim()}:{portNumber}{path.Trim()}";
         }
         catch (Exception e)
         {
            Console.WriteLine("ConfigManager.LoadKranLiveHubUrlFromDatabase -> {0}", e.Message);
            return fallback;
         }
      }

      private string LoadParameterValue(string name, string fallback)
      {
         using SqlConnection connection = new(connectionString);
         using SqlCommand command = new("dbo.FALCOM_GetParameterValue", connection)
         {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 10
         };

         command.Parameters.Add("@Name", SqlDbType.NVarChar, 256).Value = name;

         connection.Open();
         using SqlDataReader reader = command.ExecuteReader();

         if (!reader.Read())
         {
            return fallback;
         }

         string? value = Convert.ToString(reader["Wert"])?.Trim();
         return string.IsNullOrWhiteSpace(value) ? fallback : value;
      }
   }
}
