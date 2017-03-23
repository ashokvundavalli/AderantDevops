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
            string test = ""Test"";
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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(10, 21));
        }

        [TestMethod]
        public void SqlInjectionError_System_Data_IDbCommand() {
            const string test = @"
using System;

namespace Test {
    public class Program {
        public static void Main() {
            string test = ""Test"";
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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(10, 21));
        }

        [TestMethod]
        public void SqlInjectionError_System_SqlClient_SqlCommand() {
            const string test = @"
using System;

namespace Aderant.FirmControl.DocuDraft.DataAccess {
    public class SqlBase {
        public static void Main() {
            IssueSqlNoBatch(null, null, null, false, false);
        }

        public static void IssueSqlNoBatch(
            System.Data.SqlClient.SqlConnection a,
            System.Text.StringBuilder b,
            System.Collections.Generic.List<System.Data.SqlClient.SqlParameter> c,
            bool d,
            bool e) {
            string test = ""Test"";
            using (var connection = Framework.Persistence.FrameworkDb.CreateConnection()) {
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
        public System.Data.SqlClient.SqlCommand CreateCommand() {
            return new System.Data.SqlClient.SqlCommand();
        }

        public void Dispose() {
            // Empty.
        }
    }
}

namespace System.Data.SqlClient {
    public class SqlCommand : IDisposable {
        public string CommandText { get; set; }
        public SqlParameterCollection Parameters { get; set; }

        public void Dispose() {
            // Empty.
        }
    }

    public class Sqlparameter {
        // Empty.
    }

    public sealed class SqlParameterCollection {
        public void Add(SqlParameter parameter) {
            // Empty.
        }
    }
}
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(19, 21));
        }

        [TestMethod]
        public void SqlInjectionError_StringLiteral_And_NonConstLocalVariable() {
            const string test = @"
using System;

namespace Test {
    public class Program {
        public static void Main() {
            string test = ""Test"";
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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(10, 21));
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
            string test = """";
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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(6, 27));
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
        public void SqlInjectionError_SqlQuery_Error() {
            const string test = @"
using System.Data.Entity;

namespace Test {
    public class Program {
        public static void Main() {
            string test = """";
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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(8, 13));
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
            string test = """";
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

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(6, 27));
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
    }
}
