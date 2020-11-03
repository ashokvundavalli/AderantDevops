using System;
using Aderant.Build.Analyzer;
using Aderant.Build.Analyzer.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UnitTest.Aderant.Build.Analyzer.Verifiers;

namespace UnitTest.Aderant.Build.Analyzer.Tests {
    [TestClass]
    public class SuppressionAttributeTests : AderantCodeFixVerifier {
        #region Types

        private class SuppressionTestRule : RuleBase {
            public bool AnalyzeTests { private get; set; }

            public override DiagnosticDescriptor Descriptor => new DiagnosticDescriptor(
                Id,
                Title,
                MessageFormat,
                AnalyzerCategory.Syntax,
                Severity,
                true,
                Description);

            internal override DiagnosticSeverity Severity => DiagnosticSeverity.Error;

            internal override string Id => "SuppressionTestRuleId";

            internal override string Title => "Suppression Test Rule Title";

            internal override string MessageFormat => "Suppression Test Rule Message Format";

            internal override string Description => "Suppression Test Rule Description";

            public override void Initialize(AnalysisContext context) {
                context.RegisterSyntaxNodeAction(EvaluateNode, SyntaxKind.ObjectCreationExpression);
            }

            internal void EvaluateNode(SyntaxNodeAnalysisContext context) {
                if (IsAnalysisSuppressed(context.Node, Id, AnalyzeTests)) {
                    return;
                }

                context.ReportDiagnostic(Diagnostic.Create(Descriptor, Location.None));
            }
        }

        #endregion Types

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SuppressionAttributeTests" /> class.
        /// </summary>
        public SuppressionAttributeTests()
            : base(null) {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SuppressionAttributeTests" /> class.
        /// </summary>
        /// <param name="injectedRules">The injected rules.</param>
        public SuppressionAttributeTests(RuleBase[] injectedRules)
            : base(injectedRules) {
        }

        #endregion Constructors

        #region Fields

        protected override RuleBase Rule => testRule;

        private const string unexpectedDiagnosticError = "An unexpected diagnostic was reported.";

        private SuppressionTestRule testRule;
        private bool diagnosticReported;

        #endregion Fields

        #region Test Administration

        [TestInitialize]
        public void TestInitialize() {
            testRule = new SuppressionTestRule {
                AnalyzeTests = true
            };

            diagnosticReported = false;
        }

        private void EvaluateDiagnostic(Diagnostic diagnostic) {
            if (!diagnosticReported) {
                diagnosticReported = true;
            }
        }

        #endregion Test Administration

        #region Tests

        [TestMethod]
        public void SuppressionAttribute_Diagnostic() {
            const string code = @"
namespace Test {
    public class TestClass {
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsTrue(diagnosticReported, "The expected diagnostic was not reported.");
        }

        [TestMethod]
        public void SuppressionAttribute_Diagnostic_TestClass_FullyQualified_NoAttributeText() {
            const string code = @"
namespace Test {
    [Microsoft.VisualStudio.TestTools.TestClass]
    public class TestClass {
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsTrue(diagnosticReported, "The expected diagnostic was not reported.");
        }

        [TestMethod]
        public void SuppressionAttribute_Diagnostic_TestClass_FullyQualified_AttributeText() {
            const string code = @"
namespace Test {
    [Microsoft.VisualStudio.TestTools.TestClassAttribute]
    public class TestClass {
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsTrue(diagnosticReported, "The expected diagnostic was not reported.");
        }

        [TestMethod]
        public void SuppressionAttribute_Diagnostic_TestClass_TypeQualified_NoAttributeText() {
            const string code = @"
using Microsoft.VisualStudio.TestTools;

namespace Test {
    [TestClass]
    public class TestClass {
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsTrue(diagnosticReported, "The expected diagnostic was not reported.");
        }

        [TestMethod]
        public void SuppressionAttribute_Diagnostic_TestClass_TypeQualified_AttributeText() {
            const string code = @"
using Microsoft.VisualStudio.TestTools;

namespace Test {
    [TestClassAttribute]
    public class TestClass {
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsTrue(diagnosticReported, "The expected diagnostic was not reported.");
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Class_TestClass_FullyQualified_NoAttributeText() {
            const string code = @"
namespace Test {
    [Microsoft.VisualStudio.TestTools.TestClass]
    public class TestClass {
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.AnalyzeTests = false;

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Class_TestClass_TypeQualified_NoAttributeText() {
            const string code = @"
using Microsoft.VisualStudio.TestTools;

namespace Test {
    [TestClass]
    public class TestClass {
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.AnalyzeTests = false;

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Class_TestClass_FullyQualified_AttributeText() {
            const string code = @"
namespace Test {
    [Microsoft.VisualStudio.TestTools.TestClassAttribute]
    public class TestClass {
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.AnalyzeTests = false;

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Class_TestClass_TypeQualified_AttributeText() {
            const string code = @"
using Microsoft.VisualStudio.TestTools;

namespace Test {
    [TestClassAttribute]
    public class TestClass {
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.AnalyzeTests = false;

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_RuleSpecific_FullyQualified_AttributeText_FirstMessage() {
            const string code = @"
namespace Test {
    public class TestClass {
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("""", ""SuppressionTestRuleId"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_RuleSpecific_TypeQualified_AttributeText_FirstMessage() {
            const string code = @"
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class TestClass {
        [SuppressMessageAttribute("""", ""SuppressionTestRuleId"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_RuleSpecific_FullyQualified_AttributeText_SecondMessage() {
            const string code = @"
namespace Test {
    public class TestClass {
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("""", ""SuppressionTestRuleId"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_RuleSpecific_TypeQualified_AttributeText_SecondMessage() {
            const string code = @"
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class TestClass {
        [SuppressMessageAttribute("""", ""SuppressionTestRuleId"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_RuleSpecific_FullyQualified_NoAttributeText_FirstMessage() {
            const string code = @"
namespace Test {
    public class TestClass {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("""", ""SuppressionTestRuleId"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_RuleSpecific_TypeQualified_NoAttributeText_FirstMessage() {
            const string code = @"
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class TestClass {
        [SuppressMessage("""", ""SuppressionTestRuleId"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_RuleSpecific_FullyQualified_NoAttributeText_SecondMessage() {
            const string code = @"
namespace Test {
    public class TestClass {
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("""", ""SuppressionTestRuleId"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_RuleSpecific_TypeQualified_NoAttributeText_SecondMessage() {
            const string code = @"
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class TestClass {
        [SuppressMessage("""", ((""SuppressionTestRuleId"")))]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_GeneratedCode_FullyQualified_NoAttributeText() {
            const string code = @"
namespace Test {
    public class TestClass {
        [System.CodeDom.Compiler.GeneratedCode(""Tool"", ""Version"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_GeneratedCode_TypeQualified_NoAttributeText() {
            const string code = @"
using System.CodeDom.Compiler;

namespace Test {
    public class TestClass {
        [System.CodeDom.Compiler.GeneratedCode(""Tool"", ""Version"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_GeneratedCode_FullyQualified_AttributeText() {
            const string code = @"
namespace Test {
    public class TestClass {
        [System.CodeDom.Compiler.GeneratedCodeAttribute(""Tool"", ""Version"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_GeneratedCode_TypeQualified_AttributeText() {
            const string code = @"
using System.CodeDom.Compiler;

namespace Test {
    public class TestClass {
        [System.CodeDom.Compiler.GeneratedCodeAttribute(""Tool"", ""Version"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_RuleSpecific_TypeQualified_Duplicate_Sequential() {
            const string code = @"
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class TestClass {
        [SuppressMessage("""", ""SuppressionTestRuleId""), SuppressMessage("""", ""SuppressionTestRuleId"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_RuleSpecific_TypeQualified_Duplicate_Stacked() {
            const string code = @"
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class TestClass {
        [SuppressMessage("""", ""SuppressionTestRuleId"")]
        [SuppressMessage("""", ""SuppressionTestRuleId"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_RuleSpecific_TypeQualified_Duplicate_SequentialStacked() {
            const string code = @"
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class TestClass {
        [SuppressMessage("""", ""SuppressionTestRuleId""), SuppressMessage("""", ""SuppressionTestRuleId"")]
        [SuppressMessage("""", ""SuppressionTestRuleId""), SuppressMessage("""", ""SuppressionTestRuleId"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_RuleSpecific_TypeQualified_Multi_Sequential_First() {
            const string code = @"
using System;
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class TestClass {
        [SuppressMessage("""", ""SuppressionTestRuleId""), Obsolete]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_RuleSpecific_TypeQualified_Multi_Sequential_Last() {
            const string code = @"
using System;
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class TestClass {
        [Obsolete, SuppressMessage("""", ""SuppressionTestRuleId"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_RuleSpecific_TypeQualified_Multi_Stacked_First() {
            const string code = @"
using System;
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class TestClass {
        [SuppressMessage("""", ""SuppressionTestRuleId"")]
        [Obsolete]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        [TestMethod]
        public void SuppressionAttribute_Suppressed_Method_RuleSpecific_TypeQualified_Multi_Stacked_Last() {
            const string code = @"
using System;
using System.Diagnostics.CodeAnalysis;

namespace Test {
    public class TestClass {
        [Obsolete]
        [SuppressMessage("""", ""SuppressionTestRuleId"")]
        public void TestMethod() {
            new object();
        }
    }
}
";

            var context = CreateSyntaxNodeAnalysisContext(
                code,
                EvaluateDiagnostic,
                diagnostic => true);

            testRule.EvaluateNode(context);

            Assert.IsFalse(diagnosticReported, unexpectedDiagnosticError);
        }

        #endregion Tests
    }
}
