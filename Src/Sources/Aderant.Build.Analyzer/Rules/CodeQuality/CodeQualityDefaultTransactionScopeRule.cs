using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.CodeQuality {
    internal class CodeQualityDefaultTransactionScopeRule : RuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_CodeQuality_TransactionScope";

        internal static Tuple<string, string>[] ValidSuppressionMessages = {
            new Tuple<string, string>("\"Code Quality\"", "\"Aderant_TransactionScope\""),
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

        internal override string Title => "Default Transaction Scope Error";

        internal override string MessageFormat => Description;

        internal override string Description => "Use constructor overload that specifies a TransactionScopeOption.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNodeObjectCreation, SyntaxKind.ObjectCreationExpression);
        }

        private void AnalyzeNodeObjectCreation(SyntaxNodeAnalysisContext context) {
            var node = context.Node as ObjectCreationExpressionSyntax;

            if (node == null ||
                IsAnalysisSuppressed(node, ValidSuppressionMessages)) {
                return;
            }

            string originalDefinition = (context.SemanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol)?
                .OriginalDefinition
                .ToDisplayString();

            if (string.IsNullOrWhiteSpace(originalDefinition)) {
                return;
            }

            if (string.Equals(
                    originalDefinition,
                    "System.Transactions.TransactionScope.TransactionScope()") ||
                string.Equals(
                    originalDefinition,
                    "System.Transactions.TransactionScope.TransactionScope(" +
                    "System.Transactions.TransactionScope.TransactionScopeAsyncFlowOption)")) {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Descriptor,
                        node.GetLocation()));
            }
        }

        #endregion Methods
    }
}
