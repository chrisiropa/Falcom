using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace Falcom
{
   public class SimpleSqlExecute
   {
      private readonly string connectionString;
      private readonly string stmt;
      private Exception? exception;

      public Exception? Exception
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
         stmt = string.Format(formatString, paramObjects);

         Execute();
      }

      private void Execute()
      {
         using SqlConnection sqlConnection = new SqlConnection(connectionString);

         try
         {
            sqlConnection.Open();
            SqlCommand dataCommand = new SqlCommand
            {
               Connection = sqlConnection,
               CommandText = stmt
            };

            dataCommand.ExecuteNonQuery();
         }
         catch (Exception e)
         {
            exception = e;
         }
      }
   }
}
