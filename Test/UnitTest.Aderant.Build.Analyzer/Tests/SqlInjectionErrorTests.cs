using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class SqlInjectionErrorTests : AderantCodeFixVerifier {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlInjectionErrorTests" /> class.
        /// </summary>
        public SqlInjectionErrorTests()
            : base(null) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlInjectionErrorTests" /> class.
        /// </summary>
        /// <param name="injectedRules">The injected rules.</param>
        public SqlInjectionErrorTests(RuleBase[] injectedRules)
            : base(injectedRules) {
        }

        #endregion Constructors

        #region Properties

        protected override RuleBase Rule => new SqlInjectionErrorRule();

        #endregion Properties

        #region Tests: Command Text

        [TestMethod]
        public void SqlInjectionError_CommandText_MethodParameter_Diagnostic_MutableData() {
            const string test = @"
using System.Data.SqlClient;

namespace Test {
    public class Program {
        public static void Main() {
            string test = Bar();

            Execute(test);
        }

        private static void Execute(string sql) {
            using (var connection = new SqlConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
            }
        }

        public static string Bar() {
            return string.Empty;
        }
    }
}
";

            VerifyCSharpDiagnostic(test, GetDiagnostic(9, 21));
        }

        [TestMethod]
        public void SqlInjectionError_CommandText_MethodParameter_Diagnostic_MethodNotPrivate() {
            const string test = @"
using System.Data.SqlClient;

namespace Test {
    public class Program {
        public static void Main() {
            Execute(""Test"");
        }

        public static void Execute(string sql) {
            using (var connection = new SqlConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
";

            VerifyCSharpDiagnostic(test, GetDiagnostic(13, 21));
        }

        [TestMethod]
        public void SqlInjectionError_CommandText_MethodParameter_Diagnostic_PartialClass() {
            const string test = @"
using System.Data.SqlClient;

namespace Test {
    public class Program {
        public static void Main() {
            // Empty.
        }
    }

    public static partial class PartClass {
        private static void Foo() {
            Execute(""Test"");
        }

        private static void Execute(string sql) {
            using (var connection = new SqlConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = sql;
                    command.ExecuteNonQuery();
                }
            }
        }
    }
}
";

            VerifyCSharpDiagnostic(test, GetDiagnostic(19, 21));
        }

        [TestMethod]
        public void SqlInjectionError_CommandText_MethodParameter_NoDiagnostic() {
            const string test = @"
using System.Data.SqlClient;

namespace Test {
    public class Program {
        public static void Main() {
            using (var connection = new SqlConnection()) {
                Execute(""Test"", connection);
            }
        }

        private static void Execute(string sql, SqlConnection connection) {
            using (var command = connection.CreateCommand()) {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        #endregion Tests: Command Text

        #region Tests: System.Data

        [TestMethod]
        public void SqlInjectionError_System_Data_Common_DbCommand() {
            const string test = @"
using System;

namespace Test {
    public class Program {
        public static void Main() {
            Foo(""Test"");
        }

        public void Foo(string test) {
            using (var connection = Aderant.Framework.Persistence.FrameworkDb.CreateConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = test;
                }
            }
        }
    }
}

namespace Aderant.Framework.Persistence {
    public class FrameworkDb {
        public static Connection CreateConnection() {
            return new Connection();
        }
    }

    public class Connection : IDisposable {
        public System.Data.Common.DbCommand CreateCommand() {
            return new System.Data.Common.DbCommand();
        }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Data.Common {
    public class DbCommand : IDisposable {
        public string CommandText { get; set; }

        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(test, GetDiagnostic(13, 21));
        }

        [TestMethod]
        public void SqlInjectionError_System_Data_IDbCommand() {
            const string test = @"
using System;

namespace Test {
    public class Program {
        public static void Main() {
            Foo(""Test"");
        }

        public static void Foo(string test) {
            using (var connection = Aderant.Framework.Persistence.FrameworkDb.CreateConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = test;
                }
            }
        }
    }
}

namespace Aderant.Framework.Persistence {
    public class FrameworkDb {
        public static Connection CreateConnection() {
            return new Connection();
        }
    }

    public class Connection : IDisposable {
        public System.Data.IDbCommand CreateCommand() {
            return new System.Data.IDbCommand();
        }

        public void Dispose() {
            // Empty
        }
    }
}

namespace System.Data {
    public class IDbCommand : IDisposable {
        public string CommandText { get; set; }

        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(test, GetDiagnostic(13, 21));
        }

        #endregion Tests: System.Data

        #region Tests: System.SqlClient

        [TestMethod]
        public void SqlInjectionError_System_SqlClient_SqlCommand() {
            const string test = @"
using System;

namespace Test {
    public class Program {
        public static void Main() {
            Foo(""Test"");
        }

        public static void Foo(string test) {
            using (var connection = Aderant.Framework.Persistence.FrameworkDb.CreateConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = test;
                }
            }
        }
    }
}

namespace Aderant.Framework.Persistence {
    public class FrameworkDb {
        public static Connection CreateConnection() {
            return new Connection();
        }
    }

    public class Connection : IDisposable {
        public System.Data.IDbCommand CreateCommand() {
            return new System.Data.SqlClient.SqlCommand();
        }

        public void Dispose() {
            // Empty
        }
    }
}

namespace System.Data.SqlClient {
    public class SqlCommand : IDisposable {
        public string CommandText { get; set; }

        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(test, GetDiagnostic(13, 21));
        }

        #endregion Tests: System.SqlClient

        #region Tests: String Literals

        [TestMethod]
        public void SqlInjectionError_StringLiteral_And_NonConstLocalVariable() {
            const string test = @"
using System;

namespace Test {
    public class Program {
        public static void Main() {
            Foo("""");
        }

        public static void Foo(string test) {
            using (var connection = Aderant.Framework.Persistence.FrameworkDb.CreateConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = ""Test"" + ""Test"" + test;
                }
            }
        }
    }
}

namespace Aderant.Framework.Persistence {
    public class FrameworkDb {
        public static Connection CreateConnection() {
            return new Connection();
        }
    }

    public class Connection : IDisposable {
        public System.Data.IDbCommand CreateCommand() {
            return new System.Data.IDbCommand();
        }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Data {
    public class IDbCommand : IDisposable {
        public string CommandText { get; set; }

        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(test, GetDiagnostic(13, 21));
        }

        [TestMethod]
        public void SqlInjectionError_StringLiteral_And_NonConstantField() {
            const string test = @"
using System;

namespace Test {
    public class Program {
        private static string test = ""Test"";

        public static void Main() {
            using (var connection = Aderant.Framework.Persistence.FrameworkDb.CreateConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = ""Test"" + ""Test"" + test;
                }
            }
        }
    }
}

namespace Aderant.Framework.Persistence {
    public class FrameworkDb {
        public static Connection CreateConnection() {
            return new Connection();
        }
    }
    public class Connection : IDisposable {
        public System.Data.IDbCommand CreateCommand() {
            return new System.Data.IDbCommand();
        }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Data {
    public class IDbCommand : IDisposable {
        public string CommandText { get; set; }

        public void Dispose() {
            // Empty.
        }
    }
}
";
            VerifyCSharpDiagnostic(test, GetDiagnostic(11, 21));
        }

        #endregion Tests: String Literals

        #region Tests: No Diagnostic

        [TestMethod]
        public void SqlInjectionError_NoDiagnostic() {
            const string test = @"
using System;

namespace Test {
    public class Program {
        public static void Main() {
            using (var connection = Aderant.Framework.Persistence.FrameworkDb.CreateConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = ""Test"";
                }
            }
        }
    }
}

namespace Aderant.Framework.Persistence {
    public class FrameworkDb {
        public static Connection CreateConnection() {
            return new Connection();
        }
    }

    public class Connection : IDisposable {
        public Some.Test.Foo.Bar CreateCommand() {
            return new Some.Test.Foo.Bar();
        }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace Some.Test.Foo {
    public class Bar : IDisposable {
        public string CommandText { get; set; }

        public void Dispose() {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionError_NoDiagnostic_CommandText() {
            const string test = @"
using System.Data;
using System.Data.SqlClient;

namespace Test {
    public class Program {
        internal string TestProp {
            get {
                if (true) {
                    using (var connection = new SqlConnection(""SomeConnectionString"")) {
                        using (var command = connection.CreateCommand()) {
                            command.CommandType = CommandType.StoredProcedure;
                            command.CommandText = ""[configuration].[GetSingleConfigurationValue]"";
                            command.Parameters.AddWithValue(""path"", ""Technical.Framework"");
                            command.Parameters.AddWithValue(""name"", ""SomeConfigValue"");
                        }
                    }
                }

                return string.Empty;
            }
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        #endregion Tests: No Diagnostic

        #region Tests: New SQL Command

        [TestMethod]
        public void SqlInjectionError_NewSqlCommand_Diagnostic() {
            const string test = @"
using System.Data.SqlClient;

namespace Test {
    public class Program {
        public static void Main() {
            Foo("""");
        }

        public static void Foo(string test) {
            var command = new SqlCommand(test);

            command.Dispose();
        }
    }
}
";

            VerifyCSharpDiagnostic(test, GetDiagnostic(11, 27));
        }

        [TestMethod]
        public void SqlInjectionError_NewSqlCommand_NoDiagnostic() {
            const string test = @"
using System.Data.SqlClient;

namespace Test {
    public class Program {
        public static void Main() {
            const string test = """";
            var command = new SqlCommand(test);

            command.Dispose();
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionError_NewSqlCommand_StaticField_Diagnostic() {
            const string test = @"
using System.Data.SqlClient;

namespace Test {
    public class Program {
        public static void Main() {
            var command = new SqlCommand(StaticClass.StaticString);

            command.Dispose();
        }
    }

    public static class StaticClass {
        public static string StaticString = ""Test"";
    }
}
";

            VerifyCSharpDiagnostic(test, GetDiagnostic(7, 27));
        }

        [TestMethod]
        public void SqlInjectionError_NewSqlCommand_StaticField_NoDiagnostic() {
            const string test = @"
using System.Data.SqlClient;

namespace Test {
    public class Program {
        public static void Main() {
            var command = new SqlCommand(StaticClass.StaticString);

            command.Dispose();
        }
    }

    public static class StaticClass {
        public static readonly string StaticString = ""Test"";
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionError_NewSqlCommand_ObjectInitializer_Diagnostic() {
            const string test = @"
using System.Data.SqlClient;

namespace Test {
    public class Program {
        public static void Main() {
            Foo("""");
        }

        public static Foo(string test) {
            var command = new SqlCommand {
                CommandText = test,
                CommandTimeout = 0
            };

            command.Dispose();
        }
    }
}
";

            VerifyCSharpDiagnostic(test, GetDiagnostic(11, 27));
        }

        [TestMethod]
        public void SqlInjectionError_NewSqlCommand_ObjectInitializer_NoDiagnostic() {
            const string test = @"
using System.Data.SqlClient;

namespace Test {
    public class Program {
        public static void Main() {
            var command = new SqlCommand {
                CommandText = """",
                CommandTimeout = 0
            };

            command.Dispose();

            command = new SqlCommand {
                CommandTimeout = 0,
                CommandText = """"
            }

            command.Dispose();
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionError_NewSqlCommand_Constructor_Diagnostic() {
            const string test = @"
using System.Data;
using System.Data.SqlClient;

namespace Test {
    public class Program {
        public static void Main() {
            // Empty.
        }
    }

    internal class Receive : System.IDisposable {
        internal SqlCommand Command { get; }

        internal Receive(string sourcePath, int timeout, SqlConnection connection) {
            Command = new SqlCommand(sourcePath, connection) { CommandType = CommandType.StoredProcedure };
            Command.Parameters.Add(new SqlParameter(""@eventSourcePath"", SqlDbType.NVarChar, 255) { Direction = ParameterDirection.Input, Value = sourcePath });
            Command.Parameters.Add(new SqlParameter(""@message"", SqlDbType.Xml) { Direction = ParameterDirection.Output });
            Command.Parameters.Add(new SqlParameter(""@timeout"", SqlDbType.Int) { Direction = ParameterDirection.Input, Value = timeout });
            connection.Dispose();
        }

        public void Dispose() {
            Command.Dispose();
        }
    }
}
";

            VerifyCSharpDiagnostic(test, GetDiagnostic(16, 23));
        }

        [TestMethod]
        public void SqlInjectionError_NewSqlCommand_Constructor_NoDiagnostic() {
            const string test = @"
using System.Data;
using System.Data.SqlClient;

namespace Test {
    public class Program {
        public static void Main() {
            // Empty.
        }
    }

    internal class Receive : System.IDisposable {
        internal SqlCommand Command { get; }

        internal Receive(string sourcePath, int timeout, SqlConnection connection) {
            Command = new SqlCommand(""Messaging.GetNextEventFromQueue"", connection) { CommandType = CommandType.StoredProcedure };
            Command.Parameters.Add(new SqlParameter(""@eventSourcePath"", SqlDbType.NVarChar, 255) { Direction = ParameterDirection.Input, Value = sourcePath });
            Command.Parameters.Add(new SqlParameter(""@message"", SqlDbType.Xml) { Direction = ParameterDirection.Output });
            Command.Parameters.Add(new SqlParameter(""@timeout"", SqlDbType.Int) { Direction = ParameterDirection.Input, Value = timeout });
            connection.Dispose();
        }

        public void Dispose() {
            Command.Dispose();
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        #endregion Tests: New SQL Command

        #region Tests: SQL Query

        [TestMethod]
        public void SqlInjectionError_SqlQuery_Error() {
            const string test = @"
using System.Data.Entity;

namespace Test {
    public class Program {
        public static void Main() {
            Foo("""");
        }

        public static void Foo(string test) {
            new Database().SqlQuery<int>(test);
        }
    }
}

namespace System.Data.Entity {
    public class Database {
        public void SqlQuery<TElement>(string s) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(test, GetDiagnostic(11, 13));
        }

        [TestMethod]
        public void SqlInjectionError_SqlQuery_NoDiagnostic() {
            const string test = @"
using System.Data.Entity;

namespace Test {
    public class Program {
        public static void Main() {
            const string test = """";
            new Database().SqlQuery<int>(test);
        }
    }
}

namespace System.Data.Entity {
    public class Database {
        public void SqlQuery<TElement>(string s) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        #endregion Tests: SQL Query

        #region Tests: Conditional Expression

        [TestMethod]
        public void SqlInjectionError_ConditionalExpression_Diagnostic() {
            const string test = @"
namespace Test {
    public class Program {
        public static void Main() {
            Foo("""");
        }

        public static void Foo(string test) {
            var command = new SqlCommand(true ? test : """");
        }
    }

    public class SqlCommand {
        public SqlCommand(string test) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(test, GetDiagnostic(9, 27));
        }

        [TestMethod]
        public void SqlInjectionError_ConditionalExpression_NoDiagnostic() {
            const string test = @"
namespace Test {
    public class Program {
        public static void Main() {
            const string test = """";
            var command = new SqlCommand(true ? test : """");
        }
    }

    public class SqlCommand {
        public SqlCommand(string test) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        #endregion Tests: Conditional Expression

        #region Tests: Properties

        [TestMethod]
        public void SqlInjectionError_Property_NoDiagnostic() {
            const string test = @"
using System;
using TestApp.Properties;

namespace Test {
    public class Program {
        public static void Main() {
            using (var connection = Aderant.Framework.Persistence.FrameworkDb.CreateConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = Resources.TestString;
                }
            }
        }
    }
}

namespace Aderant.Framework.Persistence {
    public class FrameworkDb {
        public static Connection CreateConnection() {
            return new Connection();
        }
    }

    public class Connection : IDisposable {
        public System.Data.Common.DbCommand CreateCommand() {
            return new System.Data.Common.DbCommand();
        }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Data.Common {
    public class DbCommand : IDisposable {
        public string CommandText { get; set; }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace TestApp.Properties {
    public static class Resources {
        public static string TestString { get; }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionError_Property_LongForm_NoDiagnostic() {
            const string test = @"
using System;

namespace Test {
    public class Program {
        public static void Main() {
            using (var connection = Aderant.Framework.Persistence.FrameworkDb.CreateConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = TestApp.Properties.Resources.TestString;
                }
            }
        }
    }
}

namespace Aderant.Framework.Persistence {
    public class FrameworkDb {
        public static Connection CreateConnection() {
            return new Connection();
        }
    }

    public class Connection : IDisposable {
        public System.Data.Common.DbCommand CreateCommand() {
            return new System.Data.Common.DbCommand();
        }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Data.Common {
    public class DbCommand : IDisposable {
        public string CommandText { get; set; }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace TestApp.Properties {
    public static class Resources {
        public static string TestString { get; }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionError_Field_NoDiagnostic() {
            const string test = @"
using System;
using TestApp.Properties;

namespace Test {
    public class Program {
        public static void Main() {
            using (var connection = Aderant.Framework.Persistence.FrameworkDb.CreateConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = Resources.TestString;
                }
            }
        }
    }
}

namespace Aderant.Framework.Persistence {
    public class FrameworkDb {
        public static Connection CreateConnection() {
            return new Connection();
        }
    }

    public class Connection : IDisposable {
        public System.Data.Common.DbCommand CreateCommand() {
            return new System.Data.Common.DbCommand();
        }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Data.Common {
    public class DbCommand : IDisposable {
        public string CommandText { get; set; }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace TestApp.Properties {
    public static class Resources {
        public const string TestString;
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionError_Field_LongForm_NoDiagnostic() {
            const string test = @"
using System;

namespace Test {
    public class Program {
        public static void Main() {
            using (var connection = Aderant.Framework.Persistence.FrameworkDb.CreateConnection()) {
                using (var command = connection.CreateCommand()) {
                    command.CommandText = TestApp.Properties.Resources.TestString;
                }
            }
        }
    }
}

namespace Aderant.Framework.Persistence {
    public class FrameworkDb {
        public static Connection CreateConnection() {
            return new Connection();
        }
    }

    public class Connection : IDisposable {
        public System.Data.Common.DbCommand CreateCommand() {
            return new System.Data.Common.DbCommand();
        }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Data.Common {
    public class DbCommand : IDisposable {
        public string CommandText { get; set; }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace TestApp.Properties {
    public static class Resources {
        public static readonly string TestString;

        static Resources() {
            TestString = ""Foo"";
        }
    }
}";

            VerifyCSharpDiagnostic(test);
        }

        #endregion Tests: Properties

        #region Tests: Non-Constant Assignment

        [TestMethod]
        public void SqlInjectionError_NonConstantAssignment_DataImmutable() {
            const string test = @"
using System.Data.Entity;

namespace Test {
    public class Program {
        public const string TheConst = ""CONSTANT"";

        public static void Main() {
            string temp0 = TheConst + ""0"";
            string temp1 = temp0 + ""1"";

            temp1 = temp0 + ""1"";

            string temp2 = true ? temp1 + ""2"" : """";

            new Database().SqlQuery<int>(temp2);
        }
    }
}

namespace System.Data.Entity {
    public class Database {
        public void SqlQuery<TElement>(string s) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionError_NonConstantAssignment_DataImmutable_AdditionalAssignment() {
            const string test = @"
using System.Data.Entity;

namespace Test {
    public class Program {
        public const string TheConst = ""CONSTANT"";

        public static void Main() {
            Foo(""Foo"");
        }

        public static Foo(string test) {
            string temp0 = TheConst + ""0"";
            string temp1 = temp0 + ""1"";

            temp1 = temp0 + ""1"";

            string temp2 = true ? temp1 + ""2"" : """";

            new Database().SqlQuery<int>(temp2);

            temp2 = test;
        }
    }
}

namespace System.Data.Entity {
    public class Database {
        public void SqlQuery<TElement>(string s) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionError_NonConstantAssignment_DataImmutable_MethodReturn() {
            const string test = @"
using System.Data.Entity;

namespace Test {
    public class Program {
        public const string TheConst = ""CONSTANT"";

        public static void Main() {
            string temp0 = TheConst + ""0"";
            string temp1 = temp0 + Bar();

            temp1 = TheConst;

            string temp2 = true ? temp1 + ""2"" : """";

            new Database().SqlQuery<int>(temp2);
        }

        public static string Bar() {
            return string.Empty;
        }
    }
}

namespace System.Data.Entity {
    public class Database {
        public void SqlQuery<TElement>(string s) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionError_NonConstantAssignment_DataMutable() {
            const string test = @"
using System.Data.Entity;

namespace Test {
    public class Program {
        public const string TheConst = ""CONSTANT"";

        public static void Main() {
            Foo(""Foo"");
        }

        public static void Foo(string temp1) {
            string temp0 = TheConst + ""0"";
            temp1 = temp0 + ""1"";

            string temp2 = true ? temp1 + ""2"" : """";

            new Database().SqlQuery<int>(temp2);
        }
    }
}

namespace System.Data.Entity {
    public class Database {
        public void SqlQuery<TElement>(string s) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionError_NonConstantAssignment_DataMutable_MethodReturn() {
            const string test = @"
using System.Data.Entity;

namespace Test {
    public class Program {
        public const string TheConst = ""CONSTANT"";

        public static void Main() {
            string temp0 = TheConst + ""0"";
            string temp1 = temp0 + Bar();
            string temp2 = true ? temp1 + ""2"" : """";

            new Database().SqlQuery<int>(temp2);
        }

        public static string Bar() {
            return string.Empty;
        }
    }
}

namespace System.Data.Entity {
    public class Database {
        public void SqlQuery<TElement>(string s) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(test, GetDiagnostic(13, 13));
        }

        [TestMethod]
        public void SqlInjectionError_NonConstantAssignment_SelfAssignment_NoDiagnostic() {
            const string test = @"
using System.Data.Entity;

namespace Test {
    public class Program {
        public static void Main() {
            Foo();
        }

        public static void Foo() {
            string test = ""Value1"";
            test = test + "" Value2"";

            new Database().SqlQuery<int>(test);
        }
    }
}

namespace System.Data.Entity {
    public class Database {
        public void SqlQuery<TElement>(string s) {
            // Empty.
        }
    }
}
";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void SqlInjectionError_NonConstantAssignment_SelfAssignmentWithCoalesce_Diagnostic() {
            const string test = @"
using System.Data.Entity;

namespace Test {
    public class Program {
        public static void Main() {
            Foo(""FAIL"");
        }

        public static void Foo(string input) {
            string test = ""Value1"";
            test = true ? input : ""this will never happen"";

            new Database().SqlQuery<int>(test);
        }
    }
}

namespace System.Data.Entity {
    public class Database {
        public void SqlQuery<TElement>(string s) {
            // Empty.
        }
    }
}
";
            VerifyCSharpDiagnostic(test, GetDiagnostic(14, 13));
        }

        [TestMethod]
        public void SqlInjectionError_NonConstantAssignment_IfStatementSelfAssignment_NoDiagnostic() {
            const string test = @"
using System.Data.Entity;

namespace Test {
    public class Program {
        public static void Main() {
            Foo(""FAIL"", true);
        }

        public static void Foo(string input, bool testBool) {
            string test = ""Value1"";
            
            if(testBool){
                test = test + "" Value2"";
            }

            new Database().SqlQuery<int>(test);
        }
    }
}

namespace System.Data.Entity {
    public class Database {
        public void SqlQuery<TElement>(string s) {
            // Empty.
        }
    }
}
";
            VerifyCSharpDiagnostic(test);

        }

        #endregion Tests: Non-Constant Assignment
    }
}
