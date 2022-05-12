using System.Threading.Tasks;
using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.CodeQuality {
    [TestClass]
    public class CodeQualitySystemDiagnosticsTests : AderantCodeFixVerifier<CodeQualitySystemDiagnosticsRule> {

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

        #region Tests

        [TestMethod]
        public async Task CodeQualitySystemDiagnostics_Break() {
            const string code = @"
            System.Diagnostics.Debugger.Break();
";

            await VerifyCSharpDiagnostic(
                InsertCode(code),
                // Error: System.Diagnostics.Debugger.Break();
                GetDiagnostic(6, 13, "System.Diagnostics.Debugger.Break()"));
        }

        [TestMethod]
        public async Task CodeQualitySystemDiagnostics_Launch() {
            const string code = @"
            System.Diagnostics.Debugger.Launch();
";

            await VerifyCSharpDiagnostic(
                InsertCode(code),
                // Error: System.Diagnostics.Debugger.Launch();
                GetDiagnostic(6, 13, "System.Diagnostics.Debugger.Launch()"));
        }

        [TestMethod]
        public async Task CodeQualitySystemDiagnostics_TestClass() {
            const string code = @"
namespace Test {

    public class TestClass : System.Attribute {}

    [TestClass]
    public class Program {
        public static void TestMethod() {
            System.Diagnostics.Debugger.Launch();
        }
    }
}
";

            await VerifyCSharpDiagnostic(
                code,
                GetDiagnostic().WithSpan(9, 13, 9, 47).WithArguments("System.Diagnostics.Debugger.Launch()"));
        }

        #endregion Tests
    }
}
