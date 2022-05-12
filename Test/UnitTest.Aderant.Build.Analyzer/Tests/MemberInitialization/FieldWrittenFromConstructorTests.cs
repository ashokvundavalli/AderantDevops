using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Tests.MemberInitialization.TestCases;
using VerifyCS = TestHelper.CSharpAnalyzerVerifier<Aderant.Build.Analyzer.AderantAnalyzer<SonarAnalyzer.Rules.CSharp.FieldWrittenFromConstructor>>;

namespace UnitTest.Aderant.Build.Analyzer.Tests.MemberInitialization {
    [TestClass]
    public class FieldWrittenFromConstructorTests {

        public string EditorConfig {
            get { return "dotnet_code_quality.aderant.field_written_from_constructor.applies_to = derived:Aderant.Framework.Presentation.AppShell.AppShellApplication"; }
        }


        [TestMethod]
        public async Task Field_initialization_from_instance_constructor_forbidden() {
            await new VerifyCS.Test {
                EditorConfig = EditorConfig,
                TestCode = Resources.TestCode_FieldFromInstanceConstructor,
                ExpectedDiagnostics = { VerifyCS.Diagnostic().WithSpan(14, 13, 14, 43).WithSpan(11, 31, 11, 59).WithArguments("fieldFromInstanceConstructor"), }
            }.RunAsync();
        }

        [TestMethod]
        public async Task Static_field_initialization_from_instance_constructor_forbidden() {
            await new VerifyCS.Test {
                EditorConfig = EditorConfig,
                TestCode = Resources.TestCode_StaticFieldFromInstanceConstructor,
                ExpectedDiagnostics = { VerifyCS.Diagnostic().WithSpan(14, 13, 14, 49).WithSpan(11, 38, 11, 72).WithArguments("staticFieldFromInstanceConstructor"), }
            }.RunAsync();
        }


        [TestMethod]
        public async Task Field_initialization_from_static_constructor_forbidden() {
            await new VerifyCS.Test {
                EditorConfig = EditorConfig,
                TestCode = Resources.TestCode_InStaticConstructor,
                ExpectedDiagnostics = { VerifyCS.Diagnostic().WithSpan(14, 13, 14, 41).WithSpan(11, 38, 11, 64).WithArguments("fieldFromStaticConstructor"), }
            }.RunAsync();
        }

        [TestMethod]
        public async Task Delegate_type_initialization_from_constructor_allowed() {
            await VerifyCS.VerifyAnalyzerAsync(Resources.TestCode_DelegateTypeFieldFromInstanceConstructor);
        }

    }
}