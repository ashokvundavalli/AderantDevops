using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.Logging {
    public class LoggingBanExceptionWithoutMessageRule : LoggingRuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_Logging_BanExceptionWithoutMessage";

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

        internal override string Title => "Exception without message";

        internal override string MessageFormat => Description;

        internal override string Description => "Log cannot contain an exception without a message " +
                                                "to give more context to about the exception that is being logged.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeInvocationNode, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocationNode(SyntaxNodeAnalysisContext context) {
            var node = context.Node as InvocationExpressionSyntax;

            if (node == null ||
                // The below ensures that this rule
                // cannot be formally suppressed within the source code.
                // Though suppression via the GlobalSuppression.cs file,
                // and thus the automated suppression, is still honoured.
                IsAnalysisSuppressed(node, new Tuple<string, string>[0])) {
                return;
            }

            // Confirm the method being examined is actually a Log method.
            var methodSymbol = context
                .SemanticModel
                .GetSymbolInfo(node)
                .Symbol as IMethodSymbol;

            if (methodSymbol == null ||
                GetLogMethodSignature(methodSymbol) != LogMethodSignature.Exception) {
                return;
            }

            // Raise a diagnostic if an Exception is included in the log
            // without a message to give more information about the exception
            ReportDiagnostic(
                context,
                Descriptor,
                node.GetLocation(),
                node);
        }


        #endregion
    }
}
