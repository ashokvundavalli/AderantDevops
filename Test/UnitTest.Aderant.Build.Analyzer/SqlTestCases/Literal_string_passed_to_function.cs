using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Test
{
    public class Literal_string_passed_to_function
    {
        private static bool CheckColumnExpertFileNameForBill()
        {
            var exists = false;
            WorkWithReader(
                "select * from[dbo].[TBM_FMT_BL]",
                reader => exists = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).Contains("EXPERT_FILENAME")
            );
            return exists;
        }

        private static void WorkWithReader(string query, Action<SqlDataReader> action)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                        action(reader);
            }
        }

        public static string ConnectionString { get; set; }

    }
}