using System;
using System.Threading.Tasks;
using Aderant.Build.Analyzer.Rules;
using Aderant.Build.Analyzer.Rules.CodeQuality;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests.CodeQuality {
    [TestClass]
    public class CodeQualityMathRoundRuleTests : AderantCodeFixVerifier<CodeQualityMathRoundRule> {

        [TestMethod]
        public async Task MathRoundRule_InvalidMathRoundSingleNodeExpression() {
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
            await VerifyCSharpDiagnostic(
                code,
                GetDiagnostic(7, 21));

        }

        [TestMethod]
        public async Task MathRoundRule_InvalidDecimalRoundSingleNodeExpression() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private void Method() {
            var x = decimal.Round(1.09m);
        }
    }
}
";
            await VerifyCSharpDiagnostic(
                code,
                GetDiagnostic(7, 21));

        }

        [TestMethod]
        public async Task MathRoundRule_InvalidMathRoundTwoNodesExpression() {
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
            await VerifyCSharpDiagnostic(
                code,
                GetDiagnostic(7, 21));

        }


        [TestMethod]
        public async Task MathRoundRule_InvalidMathRoundThreeNodesExpression() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private void Method() {
            var min = Math.Round((decimal)((100 - 20) / 60), MidpointRounding.AwayFromZero);
        }
    }
}
";
            await VerifyCSharpDiagnostic(
                code,
                GetDiagnostic(7, 23));

        }

        [TestMethod]
        public async Task MathRoundRule_InvalidDecimalRoundExpression() {
            const string code = @"
using System;

namespace Test {
    public class TestClass {
        private void Method() {
            var min = Decimal.Round(((100 - 20) / 60), MidpointRounding.AwayFromZero);
        }
    }
}
";
            await VerifyCSharpDiagnostic(
                code,
                GetDiagnostic(7, 23));
        }

        [TestMethod]
        public async Task MathRoundRule_ValidExpression() {
            const string code = @"
using System;
namespace Test {
    public static class MathRounding {
        public static object RoundCurrencyAmount(object input1, object input2) {
            return null;
        }
    }

    public class TestClass {
        private void Method() {
            var min = MathRounding.RoundCurrencyAmount(((100 - 20) / 60), 2);
        }
    }
}
";
            await VerifyCSharpDiagnostic(
                code);
        }

    }
}
