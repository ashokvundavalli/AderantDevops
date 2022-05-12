using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.CodeQuality {
    public class CodeQualityDefaultTransactionScopeRule : RuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_CodeQuality_TransactionScope";

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

        internal override string MessageFormat => "Use constructor overload that specifies a TransactionOption " +
                                                  "with IsolationLevel assigned the value 'ReadCommitted'.";

        internal override string Description => "The default constructor for TransactionScope defaults to serializable " +
                                                "isolation level which reduces system performance and causes deadlocks. " +
                                                "Use a constructor overload that specifies a TransactionScopeOption " +
                                                "with IsolationLevel assigned the value 'ReadCommitted'.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNodeObjectCreation, SyntaxKind.ObjectCreationExpression);
        }

        private void AnalyzeNodeObjectCreation(SyntaxNodeAnalysisContext context) {
            var node = context.Node as ObjectCreationExpressionSyntax;

            if (node == null ||
                IsAnalysisSuppressed(node, DiagnosticId)) {
                return;
            }

            string originalDefinition = (context.SemanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol)?
                .OriginalDefinition
                .ToDisplayString();

            if (string.IsNullOrWhiteSpace(originalDefinition) ||
                !originalDefinition.StartsWith("System.Transactions.TransactionScope.TransactionScope")) {
                return;
            }

            if (!string.Equals(
                    originalDefinition,
                    "System.Transactions.TransactionScope.TransactionScope(" +
                    "System.Transactions.TransactionScopeOption, " +
                    "System.Transactions.TransactionOptions)")  &&
                !string.Equals(
                    originalDefinition,
                    "System.Transactions.TransactionScope.TransactionScope(" +
                    "System.Transactions.TransactionScopeOption, " +
                    "System.Transactions.TransactionOptions, " +
                    "System.Transactions.TransactionScopeAsyncFlowOption)") &&
                !string.Equals(
                    originalDefinition,
                    "System.Transactions.TransactionScope.TransactionScope(" +
                    "System.Transactions.TransactionScopeOption, " +
                    "System.Transactions.TransactionOptions, " +
                    "System.Transactions.EnterpriseServicesInteropOption)")) {
                ReportDiagnostic(context, Descriptor, node.GetLocation(), node);
                return;
            }

            InitializerExpressionSyntax initializerExpression = null;

            var arugmentList = node.ArgumentList;

            foreach (var argument in arugmentList.Arguments) {
                var objectCreationExpression = argument.Expression as ObjectCreationExpressionSyntax;

                if (objectCreationExpression == null ||
                    !string.Equals(
                        "TransactionOptions",
                        objectCreationExpression.Type.ToString(),
                        StringComparison.Ordinal)) {
                    continue;
                }

                initializerExpression = objectCreationExpression.Initializer;
                break;
            }

            if (initializerExpression == null) {
                ReportDiagnostic(context, Descriptor, node.GetLocation(), node);
                return;
            }

            var assignmentExpressions = initializerExpression.ChildNodes().OfType<AssignmentExpressionSyntax>();

            foreach (var assignmentExpression in assignmentExpressions) {
                var propertyName = assignmentExpression.Left as IdentifierNameSyntax;

                if (propertyName == null ||
                    !propertyName.Identifier.Text.Equals("IsolationLevel", StringComparison.Ordinal)) {
                    continue;
                }

                var memberAccessExpression = assignmentExpression.Right as MemberAccessExpressionSyntax;

                if (memberAccessExpression == null ||
                    !memberAccessExpression.Name.Identifier.Text.Equals("ReadCommitted", StringComparison.Ordinal)) {
                    continue;
                }

                return;
            }

            ReportDiagnostic(context, Descriptor, node.GetLocation(), node);
        }

        #endregion Methods
    }
}
