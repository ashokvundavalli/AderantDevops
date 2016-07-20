﻿using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer {
    [TestClass]
    public class PropertyChangedNoStringNonFixableTests : AderantCodeFixVerifier {

        /// <summary>
        /// Gets the rule to be verified.
        /// </summary>
        protected override RuleBase Rule => new PropertyChangedNoStringNonFixableRule();

        protected override string PreCode => PropertyChangedNoStringTests.SharedPreCode;

        [TestMethod]
        public void PropertyChange_string_refers_to_non_member() {
            var test = InsertCode(@"OnPropertyChanged(""Test3"");");

            var expected = GetDefaultDiagnostic("Test3");
            VerifyCSharpDiagnostic(test, expected);
        }
    }
}