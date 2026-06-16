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
      
      public string PythonExe { get; } = string.Empty;

      public Parameter(ConfigManager configManager, ILogger<Parameter> logger)
      {
         _configManager = configManager;
         _logger = logger;

		   try
         {
            SimpleSqlQuery query = new SimpleSqlQuery(_configManager.ConnectionString , "select * from FALCOM_PARAMETER where Name like 'PYTHON_EXE'");
            if (query.QueryResult != null)
            {
               foreach (Dictionary<string, object> row in query.QueryResult)
               {
                  try
                  {
                     PythonExe = (string)row["Wert"];
                     break;
                  }
                  catch(Exception e)
                  {
                     _logger.LogError(e, "Tabelle FALCOM_PARAMETER konnte für PYTHON_EXE nicht gelesen werden.");
                  }
               }
            }
         }
         catch (Exception e)
         {
            _logger.LogError(e, "Tabelle FALCOM_PARAMETER konnte nicht geladen werden.");
         }
	   }
	}
}
