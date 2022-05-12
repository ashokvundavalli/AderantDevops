using System.Threading.Tasks;
using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class SqlCommandWithParameters : AderantCodeFixVerifier<SqlInjectionErrorRule> {

        [TestMethod]
        public async Task When_parameters_collection_is_used_code_is_not_in_error() {
            const string test = @"
string test = """";
var command = new SqlCommand(test);
command.Parameters.AddWithValue(""a"", 1);
command.Dispose();
";

            await VerifyCSharpDiagnostic(InsertCode(test));
        }
    }
}
