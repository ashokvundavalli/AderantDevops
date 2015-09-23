using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using System.IO;
using System.Data;

namespace Aderant.Build {

    internal class SqlBackupDatabase {
        private readonly string connectionString;
        private List<SQLDataFileEntry> filesInBackup;

        public string BackupFile { get; private set; }

        public IList<SQLDataFileEntry> FilesInBackup {
            get { return new ReadOnlyCollection<SQLDataFileEntry>(filesInBackup); }
        }
        
        public SqlBackupDatabase(string server, string db) {
            connectionString = SqlDatabaseOperation.CreateConnectionString(server, db);
        }

        //Backup specified DB to path
        public void Backup(string path, string backupDB) {
            this.BackupFile = Path.Combine(path, backupDB + ".bak");
            SqlConnection conn = new SqlConnection(this.connectionString);
            
            using (conn) {
                if (conn.State != ConnectionState.Open) {
                    conn.Open();
                }

                BackupDatabase(backupDB, this.BackupFile, conn);
                filesInBackup = GetDatabaseFiles(conn);
            }
        }

        //Backup Database
        private void BackupDatabase(string backupDb, string backupPath, SqlConnection connection) {
            using (SqlCommand cmd = connection.CreateCommand()) {

                StringBuilder sb = new StringBuilder();
                sb.AppendFormat("BACKUP DATABASE [{0}] TO DISK=N'{1}'", backupDb, backupPath);
                sb.AppendLine();
                sb.AppendLine("WITH COPY_ONLY,");
                sb.AppendLine("NOFORMAT,");
                sb.AppendLine("INIT,");
                sb.AppendLine("SKIP,");
                sb.AppendLine("NOREWIND,");
                sb.AppendLine("NOUNLOAD,");
                //sb.AppendLine("COMPRESSION,");
                sb.AppendLine("STATS = 10");

                cmd.CommandText = sb.ToString();
               
                cmd.ExecuteNonQuery();
            }
        }

        //Get Files in Backup
        private static List<SQLDataFileEntry> GetDatabaseFiles(SqlConnection connection) {
            var files = new List<SQLDataFileEntry>();
            
            using (SqlCommand command = connection.CreateCommand()) {
                command.CommandText = "select * from sys.database_files";
                
                using (SqlDataReader reader = command.ExecuteReader()) {
                    while (reader.Read()) {
                        var fileEntry = new SQLDataFileEntry();
                        string physicalName = reader["physical_name"] as string;
                        int pos = physicalName.LastIndexOf("\\", StringComparison.Ordinal) + 1;
                        physicalName = physicalName.Substring(pos, physicalName.Length - pos);
                        fileEntry.Name = reader["name"].ToString();
                        fileEntry.PhysicalName = physicalName;

                        files.Add(fileEntry);
                    }
                }
            }

            return files;
        }
    }
}
