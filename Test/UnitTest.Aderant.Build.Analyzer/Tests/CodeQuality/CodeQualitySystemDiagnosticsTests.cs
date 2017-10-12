﻿using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.CodeQuality {
    [TestClass]
    public class CodeQualitySystemDiagnosticsTests : AderantCodeFixVerifier {
        #region Properties

        protected override RuleBase Rule => new CodeQualitySystemDiagnosticsRule();

        protected override string PreCode => @"
namespace Test {
    public class Program {
        public static void TestMethod() {
";

        protected override string PostCode => @"
        }
    }
}
";

        #endregion Properties

        #region Tests

        [TestMethod]
        [Ignore]
        public void CodeQualitySystemDiagnostics_Break() {
            const string code = @"
            System.Diagnostics.Debugger.Break();
";

            VerifyCSharpDiagnostic(
                InsertCode(code),
                // Error: System.Diagnostics.Debugger.Break();
                GetDiagnostic(6, 13, "System.Diagnostics.Debugger.Break()"));
        }

        [TestMethod]
        [Ignore]
        public void CodeQualitySystemDiagnostics_Launch() {
            const string code = @"
            System.Diagnostics.Debugger.Launch();
";

            VerifyCSharpDiagnostic(
                InsertCode(code),
                // Error: System.Diagnostics.Debugger.Launch();
                GetDiagnostic(6, 13, "System.Diagnostics.Debugger.Launch()"));
        }

        #endregion Tests
    }
}
