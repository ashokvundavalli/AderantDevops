using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class SqlCommandWithParameters : AderantCodeFixVerifier {
        /// <summary>
        /// Initializes a new instance of the <see cref="SqlCommandWithParameters" /> class.
        /// </summary>
        public SqlCommandWithParameters()
            : base(null) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqlCommandWithParameters" /> class.
        /// </summary>
        /// <param name="injectedRules">The injected rules.</param>
        public SqlCommandWithParameters(RuleBase[] injectedRules)
            : base(injectedRules) {
        }

        [TestMethod]
        public void When_parameters_collection_is_used_code_is_not_in_error() {
            const string test = @"
string test = """";
var command = new SqlCommand(test);
command.Parameters.AddWithValue(""a"", 1);
command.Dispose();
";

            VerifyCSharpDiagnostic(InsertCode(test));
        }

        protected override RuleBase Rule => new SqlInjectionErrorRule();
    }
}
