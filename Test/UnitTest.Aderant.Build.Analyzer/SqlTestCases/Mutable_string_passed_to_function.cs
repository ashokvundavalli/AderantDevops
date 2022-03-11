﻿using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Test
{
    public class Mutable_string_passed_to_function
    {

        private static string table;

        private static bool CheckColumnExpertFileNameForBill()
        {
            var exists = false;
            WorkWithReader("select * from[dbo]." + table);
            return exists;
        }

        public static void WorkWithReader(string query)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(query, connection))
                    using (var reader = command.ExecuteReader()) {

                    }

            }
        }

        public static string ConnectionString { get; set; }

    }
}