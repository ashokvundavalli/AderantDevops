using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class PropertyChangedNoStringNonFixableTests : AderantCodeFixVerifier {
        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyChangedNoStringNonFixableTests" /> class.
        /// </summary>
        public PropertyChangedNoStringNonFixableTests()
            : base(null) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PropertyChangedNoStringNonFixableTests" /> class.
        /// </summary>
        /// <param name="injectedRules">The injected rules.</param>
        public PropertyChangedNoStringNonFixableTests(RuleBase[] injectedRules)
            : base(injectedRules) {
        }

        /// <summary>
        /// Gets the rule to be verified.
        /// </summary>
        protected override RuleBase Rule => new PropertyChangedNoStringNonFixableRule();

        protected override string PreCode => PropertyChangedNoStringTests.SharedPreCode;

        [TestMethod]
        public void PropertyChange_string_refers_to_non_member() {
            var test = InsertCode(@"OnPropertyChanged(""Test3"");");
            var _ = typeof(Microsoft.CodeAnalysis.CSharp.Formatting.CSharpFormattingOptions);
            var expected = GetDefaultDiagnostic("Test3");
            VerifyCSharpDiagnostic(test, expected);
        }
    }
}
