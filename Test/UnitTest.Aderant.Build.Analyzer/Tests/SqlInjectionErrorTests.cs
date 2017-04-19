using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class SqlInjectionErrorTests : AderantCodeFixVerifier {
        protected override RuleBase Rule => new SqlInjectionErrorRule();

        protected override string PreCode => string.Empty;

        protected override string PostCode => string.Empty;

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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(13, 21));
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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(13, 21));
        }

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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(13, 21));
        }

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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(13, 21));
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
            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(11, 21));
        }

        [TestMethod]
        public void SqlInjectionError_NoError() {
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

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void SqlInjectionError_NewSqlCommand_Diagnostic() {
            const string test = @"
namespace Test {
    public class Program {
        public static void Main() {
            Foo("""");
        }

        public static void Foo(string test) {
            var command = new SqlCommand(test);
        }
    }

    public class SqlCommand {
        public SqlCommand(string test) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(9, 27));
        }

        [TestMethod]
        public void SqlInjectionError_NewSqlCommand_NoDiagnostic() {
            const string test = @"
namespace Test {
    public class Program {
        public static void Main() {
            const string test = """";
            var command = new SqlCommand(test);
        }
    }

    public class SqlCommand {
        public SqlCommand(string test) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(InsertCode(test));
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
        }
    }
}
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(11, 27));
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

            command = new SqlCommand {
                CommandTimeout = 0,
                CommandText = """"
            }
        }
    }
}
";

            VerifyCSharpDiagnostic(InsertCode(test));
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

    internal class Receive {
        internal SqlCommand Command { get; }

        internal Receive(string sourcePath, int timeout, SqlConnection connection) {
            Command = new SqlCommand(sourcePath, connection) { CommandType = CommandType.StoredProcedure };
            Command.Parameters.Add(new SqlParameter(""@eventSourcePath"", SqlDbType.NVarChar, 255) { Direction = ParameterDirection.Input, Value = sourcePath });
            Command.Parameters.Add(new SqlParameter(""@message"", SqlDbType.Xml) { Direction = ParameterDirection.Output });
            Command.Parameters.Add(new SqlParameter(""@timeout"", SqlDbType.Int) { Direction = ParameterDirection.Input, Value = timeout });
        }
    }
}
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(16, 23));
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

    internal class Receive {
        internal SqlCommand Command { get; }

        internal Receive(string sourcePath, int timeout, SqlConnection connection) {
            Command = new SqlCommand(""Messaging.GetNextEventFromQueue"", connection) { CommandType = CommandType.StoredProcedure };
            Command.Parameters.Add(new SqlParameter(""@eventSourcePath"", SqlDbType.NVarChar, 255) { Direction = ParameterDirection.Input, Value = sourcePath });
            Command.Parameters.Add(new SqlParameter(""@message"", SqlDbType.Xml) { Direction = ParameterDirection.Output });
            Command.Parameters.Add(new SqlParameter(""@timeout"", SqlDbType.Int) { Direction = ParameterDirection.Input, Value = timeout });
        }
    }
}
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(11, 13));
        }

        [TestMethod]
        public void SqlInjectionError_SqlQuery_NoError() {
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

            VerifyCSharpDiagnostic(InsertCode(test));
        }

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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(9, 27));
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

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void SqlInjectionError_PropertyResources() {
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
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void SqlInjectionError_PropertyResources_LongForm() {
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
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

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

            VerifyCSharpDiagnostic(InsertCode(test));
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

            VerifyCSharpDiagnostic(InsertCode(test));
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

            VerifyCSharpDiagnostic(InsertCode(test));
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

            VerifyCSharpDiagnostic(InsertCode(test));
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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(13, 13));
        }
    }
}
