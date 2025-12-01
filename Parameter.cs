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
      private readonly Logging _logger;
      
      public string PythonExe { get; } = string.Empty;

      public Parameter(ConfigManager configManager, Logging logger)
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
                     _logger.ZLog(ELF.ERROR, "Tabelle FALCOM_PARAMETER (PYTHON_EXE) -> {0}", e.Message);
                  }
               }
            }
         }
         catch (Exception e)
         {
            _logger.ZLog(ELF.ERROR, "Tabelle AAD_LEG_Behandlung_Ort_Map (2) -> {0}", e.Message);
         }
	   }
	}
}
