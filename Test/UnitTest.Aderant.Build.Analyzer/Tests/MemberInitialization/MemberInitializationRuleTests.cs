using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Tests.MemberInitialization.TestCases;
using VerifyCS = TestHelper.CSharpAnalyzerVerifier<Aderant.Build.Analyzer.AderantAnalyzer<Aderant.Build.Analyzer.Rules.MemberInitializationRule>>;

namespace UnitTest.Aderant.Build.Analyzer.Tests.MemberInitialization {
    [TestClass]
    public class MemberInitializationRuleTests {

        public string EditorConfig {
            get { return @"dotnet_code_quality.aderant.member_initialization.applies_to = derived:Aderant.Framework.Presentation.AppShell.AppShellApplication"; }
        }

        [TestMethod]
        public async Task Static_field_initialization_forbidden() {
            await new VerifyCS.Test {
                EditorConfig = EditorConfig,
                TestCode = Resources.TestCode,
                ExpectedDiagnostics = { VerifyCS.Diagnostic().WithSpan(11, 54, 11, 75), }
            }.RunAsync();
        }

    }
}