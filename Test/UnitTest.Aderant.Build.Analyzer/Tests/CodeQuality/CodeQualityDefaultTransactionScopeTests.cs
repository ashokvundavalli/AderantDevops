using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.CodeQuality {
    [TestClass]
    public class CodeQualityDefaultTransactionScopeTests : AderantCodeFixVerifier {
        #region Properties

        protected override RuleBase Rule => new CodeQualityDefaultTransactionScopeRule();

        #endregion Properties

        #region Tests

        [TestMethod]
        public void CodeQualityDefaultTransactionScope_All() {
            const string code = @"
using System.Transactions;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            var a = new TransactionScope();
            var b = new TransactionScope(
                TransactionScope.TransactionScopeOption.Test);
            var c = new TransactionScope(
                TransactionScope.TransactionScopeAsyncFlowOption.Test);
            var d = new TransactionScope(
                TransactionScope.TransactionScopeOption.Test,
                TransactionScope.TransactionScopeAsyncFlowOption.Test);
        }
    }
}

namespace System.Transactions {
    public class TransactionScope {
        public enum TransactionScopeAsyncFlowOption {
            Test
        }

        public enum TransactionScopeOption {
            Test
        }

        public TransactionScope()
            : this(TransactionScopeOption.Test) {
            // Empty
        }

        public TransactionScope(
            TransactionScopeOption scopeOption)
            : this(scopeOption, TransactionScopeAsyncFlowOption.Test) {
            // Empty
        }

        public TransactionScope(
            TransactionScopeAsyncFlowOption asyncFlowOption)
            : this(TransactionScopeOption.Test, asyncFlowOption) {
            // Empty
        }

        public TransactionScope(
            TransactionScopeOption scopeOption,
            TransactionScopeAsyncFlowOption asyncFlowOption) {
            // Empty
        }
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: var a = new TransactionScope();
                GetDiagnostic(7, 21),
                // Error: var c = new TransactionScope(
                //            TransactionScope.TransactionScopeAsyncFlowOption.Test);
                GetDiagnostic(10, 21));
        }

        #endregion Tests
    }
}
