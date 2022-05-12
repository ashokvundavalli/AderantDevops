using System.Threading.Tasks;
using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.CodeQuality {
    [TestClass]
    public class CodeQualityDefaultTransactionScopeTests : AderantCodeFixVerifier<CodeQualityDefaultTransactionScopeRule> {

        #region Tests

        [TestMethod]
        public async Task CodeQualityDefaultTransactionScope_Invalid() {
            const string code = @"
using System.Transactions;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            var errorA = new TransactionScope();

            var errorB = new TransactionScope(
                TransactionScopeOption.Test);

            var errorC = new TransactionScope(
                TransactionScopeAsyncFlowOption.Test);

            var errorD = new TransactionScope(
                TransactionScopeOption.Test,
                TransactionScopeAsyncFlowOption.Test);

            var errorE = new TransactionScope(
                TransactionScopeOption.Test,
                new TransactionOptions { IsolationLevel = IsolationLevel.Test });

            var errorF = new TransactionScope(
                TransactionScopeOption.Test,
                new TransactionOptions { IsolationLevel = IsolationLevel.Test },
                TransactionScopeAsyncFlowOption.Test);

            var errorG = new TransactionScope(
                TransactionScopeOption.Test,
                new TransactionOptions { IsolationLevel = IsolationLevel.Test },
                EnterpriseServicesInteropOption.Test);
        }
    }
}

namespace System.Transactions {
    public struct TransactionOptions {
        public IsolationLevel IsolationLevel { get; set; }

        public bool SomethingElse { get; set; }
    }

    public enum EnterpriseServicesInteropOption {
        Test
    }

    public enum IsolationLevel {
        ReadCommitted,
        Test
    }

    public enum TransactionScopeAsyncFlowOption {
        Test
    }

    public enum TransactionScopeOption {
        Test
    }

    public class TransactionScope {
        

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

        public TransactionScope(
            TransactionScopeOption scopeOption,
            TransactionOptions transactionOptions) {
            // Empty.
        }

        public TransactionScope(
            TransactionScopeOption scopeOption,
            TransactionOptions transactionOptions,
            TransactionScopeAsyncFlowOption asyncFlowOption) {
            // Empty.
        }

        public TransactionScope(
            TransactionScopeOption scopeOption,
            TransactionOptions transactionOptions,
            EnterpriseServicesInteropOption interopOption) {
            // Empty.
        }
    }
}
";

            await VerifyCSharpDiagnostic(
                code,
                // Error: var errorA = new TransactionScope();
                GetDiagnostic(7, 26),
                // Error: var errorB = new TransactionScope(
                //            TransactionScopeOption.Test);
                GetDiagnostic(9, 26),
                // Error: var errorC = new TransactionScope(
                //        TransactionScopeAsyncFlowOption.Test);
                GetDiagnostic(12, 26),
                // Error: var errorD = new TransactionScope(
                //            TransactionScopeOption.Test,
                //            TransactionScopeAsyncFlowOption.Test);
                GetDiagnostic(15, 26),
                // Error: var errorE = new TransactionScope(
                //            TransactionScopeOption.Test,
                //            new TransactionOptions { IsolationLevel = IsolationLevel.Test });
                GetDiagnostic(19, 26),
                // Error: var errorF = new TransactionScope(
                //            TransactionScopeOption.Test,
                //            new TransactionOptions { IsolationLevel = IsolationLevel.Test },
                //            TransactionScopeAsyncFlowOption.Test);
                GetDiagnostic(23, 26),
                // Error: var errorG = new TransactionScope(
                //            TransactionScopeOption.Test,
                //            new TransactionOptions { IsolationLevel = IsolationLevel.Test },
                //            EnterpriseServicesInteropOption.Test);
                GetDiagnostic(28, 26));
        }

        [TestMethod]
        public async Task CodeQualityDefaultTransactionScope_Valid() {
            const string code = @"
using System.Transactions;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            var validA = new TransactionScope(
                TransactionScopeOption.Test,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted });

            var validB = new TransactionScope(
                TransactionScopeOption.Test,
                new TransactionOptions { SomethingElse = true, IsolationLevel = IsolationLevel.ReadCommitted },
                TransactionScopeAsyncFlowOption.Test);

            var validC = new TransactionScope(
                TransactionScopeOption.Test,
                new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted, SomethingElse = true },
                EnterpriseServicesInteropOption.Test);
        }
    }
}

namespace System.Transactions {
    public struct TransactionOptions {
        public IsolationLevel IsolationLevel { get; set; }

        public bool SomethingElse { get; set; }
    }

    public enum EnterpriseServicesInteropOption {
        Test
    }

    public enum IsolationLevel {
        ReadCommitted,
        Test
    }

    public enum TransactionScopeAsyncFlowOption {
        Test
    }

    public enum TransactionScopeOption {
        Test
    }

    public class TransactionScope {
        

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

        public TransactionScope(
            TransactionScopeOption scopeOption,
            TransactionOptions transactionOptions) {
            // Empty.
        }

        public TransactionScope(
            TransactionScopeOption scopeOption,
            TransactionOptions transactionOptions,
            TransactionScopeAsyncFlowOption asyncFlowOption) {
            // Empty.
        }

        public TransactionScope(
            TransactionScopeOption scopeOption,
            TransactionOptions transactionOptions,
            EnterpriseServicesInteropOption interopOption) {
            // Empty.
        }
    }
}
";

            await VerifyCSharpDiagnostic(code);
        }

        #endregion Tests
    }
}
