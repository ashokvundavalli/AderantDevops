using System.Data;
using System.Data.SqlClient;

namespace Test {
    public class SqlCommand_using_sp_executesql_parameter_add_reports_no_diagnostic {

        private string myVariable = null;

        public void Foo() {
            var commandText = "abc" + myVariable;
            using (var connection = new SqlConnection("SomeConnectionString")) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = commandText;
                    var parameter = command.CreateParameter();
                    command.Parameters.Add(parameter);
                }
            }
        }

    }
}