using System;
using System.IO;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

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
   }

   public class ConfigManager
   {
      private readonly string connectionString;
      private readonly string logfilePath;
      private readonly string executionDirectory;


      public string ConnectionString => connectionString;

      public string LogfilePath => logfilePath;
      
      public ConfigManager(IOptions<Appsettings> appSettings)
      {
         try
         {
            var settings = appSettings.Value;
            logfilePath = settings.LogfilePath;

            AssemblyInfoWrapper iw = new AssemblyInfoWrapper();
            executionDirectory = Path.GetDirectoryName(iw.ExecutionPath);


            SqlConnectionStringBuilder csb = new SqlConnectionStringBuilder();

            csb.DataSource = settings.Server;
            csb.InitialCatalog = settings.DatabaseName;
            csb.UserID = settings.User;
            csb.Password = settings.Password;
            csb.Encrypt = false;


            connectionString = csb.ConnectionString;

            Console.WriteLine("ConnectionString = {0}", connectionString);

         }
         catch (Exception e)
         {
            Console.WriteLine("ConfigManger.ConfigManager -> {0}", e.Message);
         }
      }
   }
}
