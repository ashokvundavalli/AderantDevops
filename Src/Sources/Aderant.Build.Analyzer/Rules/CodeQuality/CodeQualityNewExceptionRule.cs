using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.CodeQuality {
    internal class CodeQualityNewExceptionRule : RuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_CodeQuality_NewException";

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

        internal override string Title => "New Exception Error";

        internal override string MessageFormat => "Illegal usage of 'new Exception()'.";

        internal override string Description => "Use a derived exception type that better describes the exception case.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNodeMethodInvocation, SyntaxKind.ObjectCreationExpression);
        }

        private void AnalyzeNodeMethodInvocation(SyntaxNodeAnalysisContext context) {
            var node = context.Node as ObjectCreationExpressionSyntax;

            if (node == null ||
                IsAnalysisSuppressed(node, DiagnosticId) ||
                !string.Equals(
                    "System.Exception",
                    context
                        .SemanticModel
                        .GetTypeInfo(node)
                        .Type
                        .ToDisplayString(),
                    StringComparison.Ordinal)) {
                return;
            }

            ReportDiagnostic(context, Descriptor, node.GetLocation(), node);
        }

        #endregion Methods
    }
}
