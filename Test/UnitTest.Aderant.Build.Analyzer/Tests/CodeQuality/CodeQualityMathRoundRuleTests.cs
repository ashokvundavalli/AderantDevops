using System;
using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.CodeQuality {
    [TestClass]
    public class CodeQualityMathRoundRuleTests : AderantCodeFixVerifier {
        #region Properties

        protected override RuleBase Rule => new CodeQualityMathRoundRule();

        #endregion Properties

        #region Tests

        [TestMethod]
        public void MathRoundRule_InvalidSingleNodeExpression() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private void Method() {
            var x = Math.Round(1.09);
        }
    }
}
";
            VerifyCSharpDiagnostic(
                code,
                GetDiagnostic(7, 21));

        }

        [TestMethod]
        public void MathRoundRule_InvalidTwoNodesExpression()
        {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private void Method() {
            var x = Math.Round(1.9, 2);
        }
    }
}
";
            VerifyCSharpDiagnostic(
                code,
                GetDiagnostic(7, 21));

        }

        [TestMethod]
        public void MathRoundRule_ValidTwoNodesExpression() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private void Method() {
            var x = Math.Round(1.09, MidpointRounding.AwayFromZero);
        }
    }
}
";
            VerifyCSharpDiagnostic(
                code);

        }

        [TestMethod]
        public void MathRoundRule_ValidThreeNodesExpression()
        {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private void Method() {
            int min = Math.Round(((100 - 20) / 60), MidpointRounding.AwayFromZero);
        }
    }
}
";
            VerifyCSharpDiagnostic(
                code);

        }

        [TestMethod]
        public void MathRoundRule_ValidThreeNodesExpression_WithSystemPrefix()
        {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private void Method() {
            int min = (int)System.Math.Round(((100 - 20) / 60), MidpointRounding.AwayFromZero);
        }
    }
}
";
            VerifyCSharpDiagnostic(
                code);

        }

        #endregion Tests

    }
}
