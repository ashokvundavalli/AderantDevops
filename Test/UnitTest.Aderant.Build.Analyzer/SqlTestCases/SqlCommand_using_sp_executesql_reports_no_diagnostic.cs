using System.Data;
using System.Data.SqlClient;

namespace Test {
    public class SqlCommand_using_sp_executesql_parameter_insert_reports_no_diagnostic {

        private string myVariable = null;

        internal string TestProp {
            get {
                if (true) {
                    var commandText = "abc" + myVariable;
                    using (var connection = new SqlConnection("SomeConnectionString")) {
                        using (var command = connection.CreateCommand()) {
                            command.CommandText = commandText;
                            command.Parameters.Insert(0, null);
                        }
                    }
                }

                return string.Empty;
            }
        }

    }
}