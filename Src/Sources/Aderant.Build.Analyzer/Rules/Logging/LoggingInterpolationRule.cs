using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.Logging {
    public class LoggingInterpolationRule : LoggingRuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_Logging_Interpolation";

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

        internal override string Title => "Invalid Interpolation";

        internal override string MessageFormat => Description;

        internal override string Description => "Illegal use of direct interpolation. " +
                                                "Interpolation of log messages must utilize " +
                                                "the 'params object[]' method signature.";

        #endregion Properties

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

            var argumentChildNodes = new List<SyntaxNode>(DefaultCapacity);

            // Argument [0] is the LogLevel, thus argument [1] is the string template.
            GetExpressionsFromChildNodes(ref argumentChildNodes, node.ArgumentList.Arguments[1]);

            for (int i = 0; i < argumentChildNodes.Count; ++i) {
                var childNode = argumentChildNodes[i];

                    // $"example {"one"}"
                if (childNode is InterpolatedStringExpressionSyntax ||
                    // "example " + "two"
                    childNode is BinaryExpressionSyntax ||
                    // string.Format("example {0}", "three")
                    GetIsInvocationStringFormat(
                        context.SemanticModel,
                        childNode as InvocationExpressionSyntax)) {
                    ReportDiagnostic(
                        context,
                        Descriptor,
                        childNode.GetLocation(),
                        childNode);
                }
            }
        }

        /// <summary>
        /// Determines if the specified <see cref="InvocationExpressionSyntax"/> is 'string.Format()'.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="node">The node.</param>
        private static bool GetIsInvocationStringFormat(
            SemanticModel model,
            InvocationExpressionSyntax node) {
            return node != null &&
                   (model.GetSymbolInfo(node).Symbol as IMethodSymbol)?
                   .OriginalDefinition?
                   .ToDisplayString()?
                   .IndexOf("string.Format(", StringComparison.OrdinalIgnoreCase) != -1;
        }
    }
}
