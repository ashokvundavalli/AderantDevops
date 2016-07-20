using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer {
    [TestClass]
    public class SetPropertyValueNoStringNonFixableTests : AderantCodeFixVerifier {

        /// <summary>
        /// Gets the rule to be verified.
        /// </summary>
        protected override RuleBase Rule => new SetPropertyValueNoStringNonFixableRule();

        protected override string PreCode => SetPropertyValueNoStringTests.SharedPreCode;

        [TestMethod]
        public void SetPropertyValue_string_refers_to_non_member() {
            var test = InsertCode(@"SetPropertyValue(""Test3"", 0, 0);");

            var expected = GetDefaultDiagnostic("Test3");
            VerifyCSharpDiagnostic(test, expected);
        }
    }
}