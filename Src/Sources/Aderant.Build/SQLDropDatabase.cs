using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.IO;
using System.Data;

namespace Aderant.Build {
    internal class SQLDropDatabase {

        private readonly string connectionString;
        
        public string BackupFile { get; private set; }
        
        public SQLDropDatabase(string server, string db) {
            connectionString = SqlDatabaseOperation.CreateConnectionString(server, db);
        }

        //Backup specified DB to path
        public void Drop(string databaseName) {
            using (SqlConnection conn = new SqlConnection(this.connectionString)) {
                using (conn) {
                    if (conn.State != ConnectionState.Open) {
                        conn.Open();
                    }

                    DropDatabase(databaseName, conn);
                }
            }
        }

        //Drop Database
        private void DropDatabase(string databaseName, SqlConnection connection) {
            using (SqlCommand cmd = connection.CreateCommand()) {
                cmd.CommandText = string.Format(@"ALTER DATABASE [{0}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                            DROP DATABASE [{0}]", databaseName);

                cmd.ExecuteNonQuery();
            }
        }
    }

    internal class SqlDatabaseOperation {

        public static string CreateConnectionString(string server, string database) {
            var builder = new SqlConnectionStringBuilder();

            builder.DataSource = server;
            builder.InitialCatalog = database;
            builder["Integrated Security"] = true;



            return builder.ToString();
        }
    }
}
