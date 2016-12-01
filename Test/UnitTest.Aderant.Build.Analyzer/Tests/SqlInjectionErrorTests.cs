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
            const string test =
@"using System;

namespace TestApp {
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

public void Dispose() { }
}
}

namespace System.Data.Common {
public class DbCommand : IDisposable {
public string CommandText { get; set; }

public void Dispose() { }
}
}
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(9, 1));
        }

        [TestMethod]
        public void SqlInjectionError_System_Data_IDbCommand() {
            const string test =
@"using System;

namespace TestApp {
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

public void Dispose() { }
}
}

namespace System.Data {
public class IDbCommand : IDisposable {
public string CommandText { get; set; }

public void Dispose() { }
}
}
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(9, 1));
        }

        [TestMethod]
        public void SqlInjectionError_System_SqlClient_SqlCommand() {
            const string test =
@"using System;

namespace TestApp {
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
public System.Data.SqlClient.SqlCommand CreateCommand() {
return new System.Data.SqlClient.SqlCommand();
}

public void Dispose() { }
}
}

namespace System.Data.SqlClient {
public class SqlCommand : IDisposable {
public string CommandText { get; set; }

public void Dispose() { }
}
}
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(9, 1));
        }

        [TestMethod]
        public void SqlInjectionError_StringLiteral_And_NonConstLocalVariable() {
            const string test =
@"using System;

namespace TestApp {
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

public void Dispose() { }
}
}

namespace System.Data {
public class IDbCommand : IDisposable {
public string CommandText { get; set; }

public void Dispose() { }
}
}
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(9, 1));
        }

        [TestMethod]
        public void SqlInjectionError_StringLiteral_And_NonConstantField() {
            const string test =
@"using System;

namespace TestApp {
public class Program {
private string test = ""Test"";
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

public void Dispose() { }
}
}

namespace System.Data {
public class IDbCommand : IDisposable {
public string CommandText { get; set; }

public void Dispose() { }
}
}
";
            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(9, 1));
        }

        [TestMethod]
        public void SqlInjectionError_NoError() {
            const string test =
@"using System;

namespace TestApp {
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

public void Dispose() { }
}
}

namespace Some.Test.Foo {
public class Bar : IDisposable {
public string CommandText { get; set; }

public void Dispose() { }
}
}
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        [TestMethod]
        public void SqlInjectionError_NewSqlCommand() {
            const string test =
@"namespace TestApp {
public class Program {
public static void Main() {
string test = """";
var command = new SqlCommand(test);
}
}
public class SqlCommand {
public SqlCommand(string test) {
}
}
}
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(5, 15));
        }

        [TestMethod]
        public void SqlInjectionError_SqlQuery() {
            const string test =
@"using System.Data.Entity;

namespace TestApp {
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
}
}
}
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(7, 1));
        }
    }
}
