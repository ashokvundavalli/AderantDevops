using System.Data;
using System.Data.SqlClient;

namespace Test
{
    public class SqlCommand_using_ctor_and_sp_executesql_reports_no_diagnostic
    {

        private string myVariable = null;

        internal string TestProp
        {
            get
            {
                if (true)
                {
                    var commandText = "abc" + myVariable;
                    using (var connection = new SqlConnection("SomeConnectionString"))
                    {
                        using (var command = new SqlCommand(commandText, connection))
                        {
                            command.Parameters.AddWithValue("path", "Technical.Framework");
                            command.Parameters.AddWithValue("name", "SomeConfigValue");
                        }
                    }
                }

                return string.Empty;
            }
        }
    }
}