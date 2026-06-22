using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Falcom
{
	public class Parameter
	{
      private readonly ConfigManager _configManager;
      private readonly ILogger<Parameter> _logger;

      public string OpcServer { get; } = string.Empty;

      public Parameter(ConfigManager configManager, ILogger<Parameter> logger)
      {
         _configManager = configManager;
         _logger = logger;

		   try
         {
            SimpleSqlQuery query = new SimpleSqlQuery(_configManager.ConnectionString, "select * from FALCOM_PARAMETER where Name like 'OpcServer'");
            if (query.QueryResult != null)
            {
               foreach (Dictionary<string, object> row in query.QueryResult)
               {
                  try
                  {
                     OpcServer = (string)row["Wert"];
                     break;
                  }
                  catch(Exception e)
                  {
                     _logger.LogError(e, "000F|Tabelle FALCOM_PARAMETER konnte für OpcServer nicht gelesen werden.");
                  }
               }
            }
         }
         catch (Exception e)
         {
            _logger.LogError(e, "0010|Tabelle FALCOM_PARAMETER konnte nicht geladen werden.");
         }
	   }
	}
}
