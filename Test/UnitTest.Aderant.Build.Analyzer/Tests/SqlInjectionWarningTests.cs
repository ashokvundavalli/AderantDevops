using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class SqlInjectionWarningTests : AderantCodeFixVerifier {
        protected override RuleBase Rule => new SqlInjectionWarningRule();

        protected override string PreCode => string.Empty;

        protected override string PostCode => string.Empty;

        [TestMethod]
        public void SqlInjectionWarning_SqlCommandText_With_Parameters_Add() {
            const string test =
@"using System;
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
command.Parameters.Add(new System.Data.SqlClient.SqlParameter());
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
public SqlParameterCollection Parameters { get; set; }
public void Dispose() { }
}
public class Sqlparameter {
}
public sealed class SqlParameterCollection {
public void Add(SqlParameter parameter) {
}
}
}
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(17, 1));
        }

        [TestMethod]
        public void SqlInjectionWarning_SqlCommandText_With_Parameters_AddRange() {
            const string test =
@"using System;
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
command.Parameters.AddRange(new[] { new System.Data.SqlClient.SqlParameter() });
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
public SqlParameterCollection Parameters { get; set; }
public void Dispose() { }
}
public class Sqlparameter {
}
public sealed class SqlParameterCollection {
public void Add(SqlParameter parameter) {
}
}
}
";

            VerifyCSharpDiagnostic(InsertCode(test), GetDiagnostic(17, 1));
        }
    }
}
