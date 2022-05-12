using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.IDisposable {
    public abstract class IDisposableRuleBaseTests<TRule> : AderantCodeFixVerifier<TRule>
        where TRule : RuleBase, new() {

        #region Tests

        [TestMethod]
        public void IDisposableRuleBase_BenignCode_ClassField() {
            const string code = @"
namespace Test {
    public class TestClass {
        private int item = new int();
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableRuleBase_BenignCode_LocalVariable() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void TestMethod() {
            int item = new int();
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableRuleBase_BenignCode_MethodInvocation() {
            const string code = @"
namespace Test {
    public class TestClass {
        private int TestMethod() {
            return new int();
        }

        private void OtherMethod() {
            int item = TestMethod();
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableRuleBase_BenignCode_ObjectCreation() {
            const string code = @"
namespace Test {
    public class TestClass {
        private void TestMethod() {
            new int();
        }
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        [TestMethod]
        public void IDisposableRuleBase_BenignCode_WhitelistedType() {
            const string code = @"
namespace Test {
    public class TestClass {
        private System.ServiceModel.ServiceHost item;
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        #endregion Tests
    }
}
