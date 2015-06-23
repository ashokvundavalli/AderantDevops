using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.IO;
using System.Data;

namespace Aderant.Build {

    internal class SqlRestoreDatabase {

        private readonly string connectionString;

        public string BackupFile { get; private set; }

        public SqlRestoreDatabase(string server, string db) {
            connectionString = SqlDatabaseOperation.CreateConnectionString(server, db);
        }

        //Backup specified DB to path
        public void Restore(string backupFile, string tempDir, string testDb, IEnumerable<SQLDataFileEntry> filesFromBackup) {
            SqlConnection conn = new SqlConnection(this.connectionString);
            using (conn) {
                if (conn.State != ConnectionState.Open) {
                    conn.Open();
                }
                var cmd = RestoreCommandBuilder(backupFile, tempDir, testDb, filesFromBackup);
                RestoreDatabase(cmd, conn);

                ChangeOwnership(testDb, conn);
            }
        }

        //Generate Restore CMD
        private SqlCommand RestoreCommandBuilder(string backupFile, string tempDir, string testDb, IEnumerable<SQLDataFileEntry> filesFromBackup) {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("USE [master]");
            sb.AppendLine(string.Format(@"RESTORE DATABASE [{0}] FROM  DISK = N'{1}' WITH REPLACE,", testDb, backupFile));

            foreach (SQLDataFileEntry item in filesFromBackup) {
                sb.AppendLine(string.Format(" MOVE N'{0}' TO N'{1}',", item.Name, Path.Combine(tempDir, item.PhysicalName)));
            }
            sb.AppendLine(@" REPLACE, NOUNLOAD, STATS = 5");

            SqlCommand command = new SqlCommand(sb.ToString());
            return command;
        }
        
        
        //Restore Database
        private void RestoreDatabase(SqlCommand cmd, SqlConnection connection) {
            using (SqlCommand command = connection.CreateCommand()) {
                command.CommandText = cmd.CommandText;
                command.CommandTimeout = (int) TimeSpan.FromMinutes(2).TotalSeconds;
                command.ExecuteNonQuery();
            }
        }

        //Alter Ownership of Database
        private void ChangeOwnership(string testdb, SqlConnection connection) {
            using (SqlCommand command = connection.CreateCommand()) {
                command.CommandText = @"ALTER DATABASE " + testdb + " SET NEW_BROKER WITH ROLLBACK IMMEDIATE";
                command.ExecuteNonQuery();
                command.CommandText = @"ALTER AUTHORIZATION ON DATABASE::" + testdb + @" TO [cmsdbo]";
                command.ExecuteNonQuery();
            }
        }
    }
}
