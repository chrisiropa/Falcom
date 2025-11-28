using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using Microsoft.Data.SqlClient;

namespace Falcom
{
   public class SimpleSqlExecute
   {
      private string connectionString;
      private string stmt;
      private Exception exception = null;
      
      public Exception Exception
      {
         get { return exception; }
      }

      private List<List<object>> queryResult = new List<List<object>>();

      public List<List<object>> QueryResult
      {
         get { return queryResult; }
      }


      public SimpleSqlExecute(string connectionString, string formatString, params object[] paramObjects)
      {
         this.connectionString = connectionString;
         this.stmt = string.Format(formatString, paramObjects);

         Execute();
      }

      private void Execute()
      {
            Microsoft.Data.SqlClient.SqlConnection sqlConnection = new SqlConnection(connectionString);

         try
         {
            sqlConnection.Open();
            SqlCommand dataCommand = new SqlCommand();
            dataCommand.Connection = sqlConnection;
            dataCommand.CommandText = stmt;
            dataCommand.ExecuteNonQuery();  
         }

         catch(Exception e)
         {
            exception = e;
         }
         finally
         {
            sqlConnection.Close();
         }
      }
   }
}
