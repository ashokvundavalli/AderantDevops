using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.CodeQuality {
    [TestClass]
    public class CodeQualityNewExceptionTests : AderantCodeFixVerifier {
        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeQualityNewExceptionTests" /> class.
        /// </summary>
        public CodeQualityNewExceptionTests()
            : base(null) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CodeQualityNewExceptionTests" /> class.
        /// </summary>
        /// <param name="injectedRules">The injected rules.</param>
        public CodeQualityNewExceptionTests(RuleBase[] injectedRules)
            : base(injectedRules) {
        }

        #endregion Constructors

        #region Properties

        protected override RuleBase Rule => new CodeQualityNewExceptionRule();

        #endregion Properties

        #region Tests

        [TestMethod]
        public void CodeQualityNewException() {
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

            VerifyCSharpDiagnostic(
                code,
                // Error: new Exception()
                GetDiagnostic(7, 19));
        }

        [TestMethod]
        public void CodeQualityNewException_SneakyException() {
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

            VerifyCSharpDiagnostic(
                code,
                // Error: new SneakyException()
                GetDiagnostic(7, 19));
        }

        [TestMethod]
        public void CodeQualityNewException_Message() {
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

            VerifyCSharpDiagnostic(
                code,
                // Error: new Exception("Test")
                GetDiagnostic(7, 19));
        }

        [TestMethod]
        public void CodeQualityNewException_Variable() {
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

            VerifyCSharpDiagnostic(
                code,
                // Error: new Exception()
                GetDiagnostic(7, 23),
                // Error: new Exception("Test")
                GetDiagnostic(8, 23));
        }

        [TestMethod]
        public void CodeQualityNewException_DerivedException() {
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

            VerifyCSharpDiagnostic(code);
        }

        #endregion Tests
    }
}
