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

        internal override string Description => "Log messages must be descriptive and provide detail that explains why an exception occurred.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeInvocationNode, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeInvocationNode(SyntaxNodeAnalysisContext context) {
            var node = context.Node as InvocationExpressionSyntax;

            if (node == null) {
                return;
            }

            // Confirm the method being examined is actually a Log method.
            var methodSymbol = context
                .SemanticModel
                .GetSymbolInfo(node)
                .Symbol as IMethodSymbol;

            if (methodSymbol == null ||
                IsSignatureValid(node, methodSymbol, context.SemanticModel)) {
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

        /// <summary>
        /// Determines whether the provided <see cref="InvocationExpressionSyntax"/> is a valid Log() method signature.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="symbol">The symbol.</param>
        /// <param name="semanticModel">The semantic model.</param>
        private static bool IsSignatureValid(
            InvocationExpressionSyntax node,
            IMethodSymbol symbol,
            SemanticModel semanticModel) {
            switch (GetLogMethodSignature(symbol)) {
                case LogMethodSignature.None: {
                    return true;
                }
                case LogMethodSignature.Message:
                case LogMethodSignature.MessageException:
                case LogMethodSignature.MessageParams: {
                    return IsMessageSignatureValid(node, semanticModel);
                }
                default: {
                    return false;
                }
            }
        }

        /// <summary>
        /// Determines whether the provided <see cref="InvocationExpressionSyntax"/> is a valid Log() message signature.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="semanticModel">The semantic model.</param>
        private static bool IsMessageSignatureValid(InvocationExpressionSyntax node, SemanticModel semanticModel) {
            if (node.ArgumentList.Arguments.Count < 2) {
                return true;
            }

            var unwrappedArgument = UnwrapParenthesizedExpressionDescending(node.ArgumentList.Arguments[1]) as ArgumentSyntax;
            if (unwrappedArgument == null) {
                return true;
            }

            var expression = UnwrapParenthesizedExpressionDescending(unwrappedArgument.Expression) as MemberAccessExpressionSyntax;
            if (expression == null) {
                return true;
            }

            var identifer = UnwrapParenthesizedExpressionDescending(expression.Expression) as IdentifierNameSyntax;
            if (identifer == null) {
                return true;
            }

            var symbol = semanticModel.GetSymbolInfo(identifer).Symbol as ILocalSymbol;
            if (symbol == null) {
                return true;
            }

            return !IsException(symbol.Type as INamedTypeSymbol);
        }

        #endregion
    }
}
