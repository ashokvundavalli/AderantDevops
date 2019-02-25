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
        public void MathRoundRule_InvalidMathRoundSingleNodeExpression() {
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
        public void MathRoundRule_InvalidMathRoundTwoNodesExpression() {
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
        public void MathRoundRule_InvalidMathRoundThreeNodesExpression() {
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
                code,
                GetDiagnostic(7, 23));

        }

        [TestMethod]
        public void MathRoundRule_InvalidDecimalRoundExpression() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private void Method() {
            int min = Decimal.Round(((100 - 20) / 60), MidpointRounding.AwayFromZero);
        }
    }
}
";
            VerifyCSharpDiagnostic(
                code,
                GetDiagnostic(7, 23));
        }

        [TestMethod]
        public void MathRoundRule_ValidExpression() {
            const string code = @"
using System;
using Aderant.Framework.Extensions;
namespace Test {
    public class TestClass {
        private void Method() {
            int min = MathRounding.RoundCurrencyAmount(((100 - 20) / 60), 2);
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
