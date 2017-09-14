using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.CodeQuality {
    internal class CodeQualitySqlQueryRule : RuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_CodeQuality_SqlQuery";

        internal static Tuple<string, string>[] ValidSuppressionMessages = {
            new Tuple<string, string>("\"Code Quality\"", "\"Aderant_SqlQuery\""),
        };

        #endregion Fields

        #region Properties

        public override DiagnosticDescriptor Descriptor => new DiagnosticDescriptor(
            Id,
            Title,
            MessageFormat,
            AnalyzerCategory.Syntax,
            Severity,
            true,
            Description);

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Error;

        internal override string Id => DiagnosticId;

        internal override string Title => "SqlQuery Error";

        internal override string MessageFormat => Description;

        internal override string Description => "Remove illegal use of 'SqlQuery()' method.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNodeMethodInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeNodeMethodInvocation(SyntaxNodeAnalysisContext context) {
            var node = (context.Node as InvocationExpressionSyntax)?.Expression as MemberAccessExpressionSyntax;

            if (node == null ||
                IsAnalysisSuppressed(node, ValidSuppressionMessages)) {
                return;
            }

            string displayString = context
                .SemanticModel
                .GetSymbolInfo(node)
                .Symbol?
                .OriginalDefinition?
                .ToDisplayString();

            if (string.IsNullOrWhiteSpace(displayString)) {
                return;
            }

            if (displayString.Equals(
                    "System.Data.Entity.Database.SqlQuery(System.Type, string, params object[])",
                    StringComparison.Ordinal) ||
                displayString.Equals(
                    "System.Data.Entity.Database.SqlQuery<TElement>(string, params object[])",
                    StringComparison.Ordinal)) {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        node.GetLocation()));
            }
        }

        #endregion Methods
    }
}
