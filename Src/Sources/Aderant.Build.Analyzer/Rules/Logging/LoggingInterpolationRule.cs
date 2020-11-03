using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.Logging {
    /// <summary>
    /// Responsible for reporting diagnostics associated with string interpolation in ILogWriter.Log() methods.
    /// </summary>
    /// <seealso cref="LoggingRuleBase" />
    public class LoggingInterpolationRule : LoggingRuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_Logging_Interpolation";

        #endregion Fields

        #region Properties

        /// <summary>
        /// Gets the descriptor.
        /// </summary>
        public override DiagnosticDescriptor Descriptor => new DiagnosticDescriptor(
            Id,
            Title,
            MessageFormat,
            AnalyzerCategory.Syntax,
            Severity,
            true,
            Description);

        /// <summary>
        /// Gets the severity.
        /// </summary>
        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Error;

        /// <summary>
        /// Gets the identifier.
        /// </summary>
        internal override string Id => DiagnosticId;

        /// <summary>
        /// Gets the title.
        /// </summary>
        internal override string Title => "Invalid Interpolation";

        /// <summary>
        /// Gets the message format.
        /// </summary>
        internal override string MessageFormat => Description;

        /// <summary>
        /// Gets the description.
        /// </summary>
        internal override string Description => "Illegal use of direct interpolation. " +
                                                "Interpolation of log messages must utilize " +
                                                "the 'params object[]' method signature.";

        #endregion Properties

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

            if (methodSymbol == null) {
                return;
            }

            var signature = GetLogMethodSignature(methodSymbol);

            // Ignore methods that aren't log methods,
            // or that are log methods, but that do not have messages.
            if (signature == LogMethodSignature.None ||
                signature == LogMethodSignature.Exception) {
                return;
            }

            // Argument [0] is the LogLevel, thus argument [1] is the string template.
            var unwrappedArgument = UnwrapParenthesizedExpressionDescending(node.ArgumentList.Arguments[1].Expression);

            if (!IsMessageValid(unwrappedArgument, context.SemanticModel)) {
                ReportDiagnostic(
                    context,
                    Descriptor,
                    unwrappedArgument.GetLocation(),
                    unwrappedArgument);
            }
        }

        /// <summary>
        /// Determines whether the specified Log() message is valid.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="model">The model.</param>
        private static bool IsMessageValid(SyntaxNode node, SemanticModel model) {
            var unwrappedNode = UnwrapParenthesizedExpressionDescending(node);

            if (GetIsConstantString(unwrappedNode, model)) {
                return true;
            }

            if (unwrappedNode is InterpolatedStringExpressionSyntax) {
                return false;
            }

            var expressions = new List<MemberAccessExpressionSyntax>();
            GetExpressionsFromChildNodes(ref expressions, unwrappedNode.Parent);

            for (int i = 0; i < expressions.Count; ++i) {
                var symbol = model.GetSymbolInfo(UnwrapParenthesizedExpressionDescending(expressions[i].Expression)).Symbol;

                var typeSymbol = symbol as INamedTypeSymbol;

                if (typeSymbol != null &&
                    typeSymbol.Name == "String" &&
                    typeSymbol.ContainingNamespace.Name == "System" &&
                    typeSymbol.ContainingAssembly.Name == "mscorlib") {
                    return false;
                }
            }

            return !(unwrappedNode is BinaryExpressionSyntax) ||
                   GetIsConstantBinaryExpression(unwrappedNode, model);
        }

        /// <summary>
        /// Determines whether the specified <see cref="SyntaxNode"/>
        /// is a binary expression that contains only constant strings.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="model">The model.</param>
        private static bool GetIsConstantBinaryExpression(SyntaxNode node, SemanticModel model) {
            var binaryExpression = node as BinaryExpressionSyntax;

            if (binaryExpression == null) {
                return false;
            }

            var left = UnwrapParenthesizedExpressionDescending(binaryExpression.Left);

            bool result = left is BinaryExpressionSyntax
                ? GetIsConstantBinaryExpression(left, model)
                : GetIsConstantString(left, model);

            if (!result) {
                return false;
            }

            var right = UnwrapParenthesizedExpressionDescending(binaryExpression.Right);

            return right is BinaryExpressionSyntax
                ? GetIsConstantBinaryExpression(right, model)
                : GetIsConstantString(right, model);
        }

        /// <summary>
        /// Determines whether the specified <see cref="SyntaxNode"/> is a constant string.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="model">The model.</param>
        private static bool GetIsConstantString(SyntaxNode node, SemanticModel model) {
            var unwrappedNode = UnwrapParenthesizedExpressionDescending(node);

            if (unwrappedNode is LiteralExpressionSyntax) {
                return true;
            }

            var invocationExpression = unwrappedNode as InvocationExpressionSyntax;
            if (invocationExpression != null) {
                return GetIsTextTranslation(invocationExpression.Expression as MemberAccessExpressionSyntax);
            }

            var identifier = unwrappedNode as IdentifierNameSyntax;

            if (identifier == null) {
                return false;
            }

            var symbol = model
                .GetSymbolInfo(identifier)
                .Symbol;

            return
                (symbol as ILocalSymbol)?.IsConst == true ||
                (symbol as IFieldSymbol)?.IsConst == true;
        }
    }
}
