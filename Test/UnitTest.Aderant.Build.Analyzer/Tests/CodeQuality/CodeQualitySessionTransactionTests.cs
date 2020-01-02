using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.CodeQuality {
    [TestClass]
    public class CodeQualitySessionTransactionTests : AderantCodeFixVerifier {
        #region Properties

        protected override RuleBase Rule => new CodeQualitySessionTransactionRule();

        #endregion Properties

        #region Tests

        [TestMethod]
        public void CodeQualitySessionTransaction_Diagnostic_NoVariable() {
            const string code = @"
namespace Test {
    public class TestClass {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(""IDisposable"", ""Aderant_IDisposableDiagnostic"")]
        public void Method(Aderant.Framework.Communication.CallContext context) {
            using (var transaction = context.Repository.GetSession().BeginTransaction()) {
                // Do stuff.
            }
        }
    }
}

namespace Aderant.Framework.Communication {
    public class CallContext {
        public Persistence.IRepository Repository { get; set; }
    }
}

namespace Aderant.Framework.Persistence {
    public interface IFrameworkSession : System.IDisposable {
        IFrameworkTransaction BeginTransaction();
    }

    public interface IFrameworkTransaction : System.IDisposable {
        // Empty.
    }

    public interface IRepository {
        IFrameworkSession GetSession();
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: context.Repository.GetSession().BeginTransaction()
                GetDiagnostic(6, 57));
        }

        [TestMethod]
        public void CodeQualitySessionTransaction_Diagnostic_Variable() {
            const string code = @"
namespace Test {
    public class TestClass {
        [System.Diagnostics.CodeAnalysis.SuppressMessage(""IDisposable"", ""Aderant_IDisposableDiagnostic"")]
        public void Method(Aderant.Framework.Communication.CallContext context) {
            using (var transaction = context.Repository.GetSession().BeginTransaction()) {
                // Do stuff.
            }
        }
    }
}

namespace Aderant.Framework.Communication {
    public class CallContext {
        public Persistence.IRepository Repository { get; set; }
    }
}

namespace Aderant.Framework.Persistence {
    public interface IFrameworkSession : System.IDisposable {
        IFrameworkTransaction BeginTransaction();
    }

    public interface IFrameworkTransaction : System.IDisposable {
        // Empty.
    }

    public interface IRepository {
        IFrameworkSession GetSession();
    }
}
";

            VerifyCSharpDiagnostic(
                code,
                // Error: context.Repository.GetSession().BeginTransaction()
                GetDiagnostic(6, 57));
        }

        [TestMethod]
        public void CodeQualitySessionTransaction_NoDiagnostic() {
            const string code = @"
namespace Test {
    public class TestClass {
        public void MethodA(Aderant.Framework.Communication.CallContext context) {
            using (var session = (context.Repository).GetSession()) {
                using (var transaction = session.BeginTransaction()) {
                    // Do stuff.
                }
            }
        }

        public void MethodB(Aderant.Framework.Communication.CallContext context) {
            using (var session = (context).Repository.GetSession()) {
                using (session.BeginTransaction()) {
                    // Do stuff.
                }
            }
        }
    }
}

namespace Aderant.Framework.Communication {
    public class CallContext {
        public Persistence.IRepository Repository { get; set; }
    }
}

namespace Aderant.Framework.Persistence {
    public interface IFrameworkSession : System.IDisposable {
        IFrameworkTransaction BeginTransaction();
    }

    public interface IFrameworkTransaction : System.IDisposable {
        // Empty.
    }

    public interface IRepository {
        IFrameworkSession GetSession();
    }
}
";

            VerifyCSharpDiagnostic(code);
        }

        #endregion Tests
    }
}
