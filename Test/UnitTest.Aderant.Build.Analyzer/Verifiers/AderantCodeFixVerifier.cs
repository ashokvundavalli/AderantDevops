using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Aderant.Build.Analyzer;
using Aderant.Build.Analyzer.Rules;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace UnitTest.Aderant.Build.Analyzer.Verifiers {
    public abstract class AderantCodeFixVerifier : CodeFixVerifier {
        #region Fields

        private readonly RuleBase[] injectedRules;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AderantCodeFixVerifier"/> class.
        /// </summary>
        /// <param name="injectedRules">The injected rules.</param>
        protected AderantCodeFixVerifier(RuleBase[] injectedRules) {
            this.injectedRules = injectedRules;
        }

        #endregion Constructors

        #region Properties

        protected virtual string PreCode => @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Diagnostics;
    using System.Data;
    using System.Data.SqlClient;

    namespace ConsoleApplication1 {
        class PROGRAM {
            static void Main(string[] args) {
                ";

        protected virtual string PostCode => @"
            }
        }
    }";

        protected int MyCodeStartsAtLine => PreCode.Split('\n').Length;

        #endregion Properties

        #region Methods

        [TestMethod]
        public void EmptyCodeWithNoViolationsPasses() {
            VerifyCSharpDiagnostic(string.Empty);
        }

        /// <summary>
        /// Get the CSharp analyzer being tested - to be implemented in non-abstract class.
        /// </summary>
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer() {
            return injectedRules != null
                ? new AderantAnalyzer(injectedRules)
                : new AderantAnalyzer(Rule);
        }

        /// <summary>
        /// Gets the rule to be verified.
        /// </summary>
        protected virtual RuleBase Rule { get; } = null;

        /// <summary>
        /// Inserts the code between the pre and post code.
        /// </summary>
        /// <param name="codeToInsert">The code to insert.</param>
        protected string InsertCode(string codeToInsert) {
            return string.Concat(PreCode, codeToInsert, PostCode);
        }

        /// <summary>
        /// Gets the diagnostic - all that changes is where the error happens.
        /// </summary>
        /// <param name="lineNumber">The line number.</param>
        /// <param name="column">The column.</param>
        /// <param name="messageParameters">The message parameters.</param>
        protected DiagnosticResult GetDiagnostic(int lineNumber, int column, params object[] messageParameters) {
            return GenerateDiagnostic(
                Rule.Id,
                Rule.MessageFormat,
                Rule.Severity,
                lineNumber,
                column,
                messageParameters);
        }

        /// <summary>
        /// Gets the default diagnostic starting at line number <see cref="MyCodeStartsAtLine"/> and column 1.
        /// </summary>
        /// <param name="messageParameters">The message parameters.</param>
        protected DiagnosticResult GetDefaultDiagnostic(params object[] messageParameters) {
            return GetDiagnostic(MyCodeStartsAtLine, 1, messageParameters);
        }

        /// <summary>
        /// Generates the diagnostic.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="messageFormat">The message format.</param>
        /// <param name="severity">The severity.</param>
        /// <param name="lineNumber">The line number.</param>
        /// <param name="column">The column.</param>
        /// <param name="messageParameters">The message parameters.</param>
        protected DiagnosticResult GenerateDiagnostic(string id, string messageFormat, DiagnosticSeverity severity, int lineNumber, int column, object[] messageParameters) {
            return new DiagnosticResult {
                Id = id,
                Message = string.Format(messageFormat, messageParameters),
                Severity = severity,
                Locations = new[] { new DiagnosticResultLocation("Test0.cs", lineNumber, column) }
            };
        }

        /// <summary>
        /// Creates a syntax node analysis context.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="reportDiagnosticAction">The report diagnostic action.</param>
        /// <param name="isSupportedDiagnosticAction">The is supported diagnostic action.</param>
        protected SyntaxNodeAnalysisContext CreateSyntaxNodeAnalysisContext(
            string code,
            Action<Diagnostic> reportDiagnosticAction,
            Func<Diagnostic, bool> isSupportedDiagnosticAction) {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            var nodes = new List<ObjectCreationExpressionSyntax>(1);
            RuleBase.GetExpressionsFromChildNodes(ref nodes, syntaxTree.GetRoot());

            return new SyntaxNodeAnalysisContext(
                nodes.First(),
                CSharpCompilation.Create("Test").AddSyntaxTrees(syntaxTree).GetSemanticModel(syntaxTree),
                new AnalyzerOptions(new ImmutableArray<AdditionalText>()),
                reportDiagnosticAction,
                isSupportedDiagnosticAction,
                CancellationToken.None);
        }

        #endregion Methods
    }
}
