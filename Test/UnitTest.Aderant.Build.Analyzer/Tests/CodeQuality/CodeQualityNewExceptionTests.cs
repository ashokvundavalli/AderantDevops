using System.Threading.Tasks;
using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.CodeQuality {
    [TestClass]
    public class CodeQualityNewExceptionTests : AderantCodeFixVerifier<CodeQualityNewExceptionRule> {

        #region Tests

        [TestMethod]
        public async Task CodeQualityNewException() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private void Method() {
            throw new Exception();
        }
    }
}
";

            await VerifyCSharpDiagnostic(
                code,
                // Error: new Exception()
                GetDiagnostic(7, 19));
        }

        [TestMethod]
        public async Task CodeQualityNewException_SneakyException() {
            const string code = @"
using SneakyException = System.Exception;

namespace Test {
    public class TestClass {
        public void TestMethod() {
            throw new SneakyException();
        }
    }
}
";

            await VerifyCSharpDiagnostic(
                code,
                // Error: new SneakyException()
                GetDiagnostic(7, 19));
        }

        [TestMethod]
        public async Task CodeQualityNewException_Message() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private void Method() {
            throw new Exception(""Test"");
        }
    }
}
";

            await VerifyCSharpDiagnostic(
                code,
                // Error: new Exception("Test")
                GetDiagnostic(7, 19));
        }

        [TestMethod]
        public async Task CodeQualityNewException_Variable() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private void Method() {
            var ex0 = new Exception();
            var ex1 = new Exception(""Test"");
        }
    }
}
";

            await VerifyCSharpDiagnostic(
                code,
                // Error: new Exception()
                GetDiagnostic(7, 23),
                // Error: new Exception("Test")
                GetDiagnostic(8, 23));
        }

        [TestMethod]
        public async Task CodeQualityNewException_DerivedException() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private void Method() {
            throw new NullReferenceException();
        }
    }
}
";

            await VerifyCSharpDiagnostic(code);
        }

        #endregion Tests
    }
}
