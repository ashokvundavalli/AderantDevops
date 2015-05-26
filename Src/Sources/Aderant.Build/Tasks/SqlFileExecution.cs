using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.TeamFoundation.Server;

namespace Aderant.Build.Tasks {
    public class SqlFileExecution : Microsoft.Build.Utilities.Task {
        [Required]
        public string EnvironmentPath { get; set; }
        [Required]
        public string SqlFile { get; set; }
        private string Database { get; set; }
        private string Server { get; set; }

        private void GetValuesFromEnvironment(string path) {
            XDocument xml = XDocument.Load(path);
            XElement environment = xml.Descendants("environment").First();
            XElement databaseServer = environment.Descendants("expertDatabaseServer").FirstOrDefault();
            if (databaseServer != null) {
                XAttribute instanceAttribute = databaseServer.Attribute("serverInstance");
                if (instanceAttribute != null && !string.IsNullOrWhiteSpace(instanceAttribute.Value)) {
                    Server = string.Format("{0}\\{1}", databaseServer.Attribute("serverName").Value, instanceAttribute.Value);
                } else {
                    Server = databaseServer.Attribute("serverName").Value;
                }
            }
            XElement connection = databaseServer.Descendants("databaseConnection").FirstOrDefault();
            if (connection != null) {
                XAttribute name = connection.Attribute("databaseName");
                if (name != null) {
                    Database = name.Value;
                }
            }
        }

        public override bool Execute() {
            GetValuesFromEnvironment(EnvironmentPath);
            string connectionString = ConnectionString(Server, Database);
            using (SqlConnection conn = new SqlConnection(connectionString)) {
                if (conn.State != ConnectionState.Open) {
                    conn.Open();
                }
                string contents = ReadFile(SqlFile);
                IEnumerable<string> commands = SplitOnGo(contents);
                ExecuteCommands(conn, commands);
            }
            return true;
        }

        private string ReadFile(string path) {
            return File.ReadAllText(path);
        }

        private static IEnumerable<string> SplitOnGo(string contents) {
            IEnumerable<string> splitOnGo = Regex.Split(contents, "\\s[Gg][Oo]\\s", RegexOptions.Multiline);
            return splitOnGo.Select(i => i.Trim(null));
        }

        private static void ExecuteCommands(SqlConnection connection, IEnumerable<string> commands) {
            using (SqlCommand sqlCommand = connection.CreateCommand()) {
                foreach (string command in commands) {
                    sqlCommand.CommandText = command;
                    sqlCommand.ExecuteNonQuery();
                }
            }

        }
        private static string ConnectionString(string server, string database) {
            SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder();
            builder.DataSource = server;
            builder.InitialCatalog = database;
            builder.ConnectTimeout = 3600;
            builder.UserID = "cmsdbo";
            builder.Password = "cmsdbo";
            return builder.ToString();
        }

    }
}
