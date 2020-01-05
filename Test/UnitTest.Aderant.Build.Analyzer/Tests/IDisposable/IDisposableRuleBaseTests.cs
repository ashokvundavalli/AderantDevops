using Aderant.Build.Analyzer.Rules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.IDisposable {
    public abstract class IDisposableRuleBaseTests : AderantCodeFixVerifier {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="IDisposableRuleBaseTests" /> class.
        /// </summary>
        /// <param name="injectedRules">The injected rules.</param>
        protected IDisposableRuleBaseTests(RuleBase[] injectedRules)
            : base(injectedRules) {
        }

        #endregion Constructors

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
