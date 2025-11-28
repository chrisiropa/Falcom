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
      private string pythonExe;


      public string PythonExe
      { 
         get 
         { 
            return pythonExe; 
         }  
      }

      public Parameter(ConfigManager configManager)
      {
         _configManager = configManager;
      }

		public void Init()
	   {
         
		   try
         {
            SimpleSqlQuery query = new SimpleSqlQuery(_configManager.ConnectionString , "select * from FALCOM_PARAMETER where Name like 'PYTHON_EXE'");
            if (query.QueryResult != null)
            {
               foreach (Dictionary<string, object> row in query.QueryResult)
               {
                  try
                  {
                     pythonExe = (string)row["Wert"];
                     break;
                  }
                  catch(Exception e)
                  {
                     Worker.TheFALOCOM.ZLog(ELF.ERROR, "Tabelle FALCOM_PARAMETER (PYTHON_EXE) -> {0}", e.Message);
                  }
               }
            }
         }
         catch (Exception e)
         {
            Worker.TheFALOCOM.ZLog(ELF.ERROR, "Tabelle AAD_LEG_Behandlung_Ort_Map (2) -> {0}", e.Message);
         }
	   }

	}


	
}
