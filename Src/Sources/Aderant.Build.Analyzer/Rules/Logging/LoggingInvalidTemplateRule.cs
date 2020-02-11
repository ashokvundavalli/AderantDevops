using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.Logging {
    public class LoggingInvalidTemplateRule : LoggingRuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_Logging_InvalidTemplate";

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

        internal override string Title => "Invalid Interpolation Template";

        internal override string MessageFormat => Description;

        internal override string Description => "Log message interpolation template is invalid. " +
                                                "Template interpolation arguments must begin with '0' and be numerically sequential.";

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
                GetLogMethodSignature(methodSymbol) != LogMethodSignature.MessageParams) {
                return;
            }

            // Argument [0] is the LogLevel, thus argument [1] is the string template.
            var templateArguments = GetInterpolationTemplateArguments(node.ArgumentList.Arguments[1]);

            int min = int.MaxValue, max = int.MinValue, count = 0;
            foreach (var templateArgument in templateArguments) {
                ++count;

                int result;
                // Overly cautious handling of argument parsing to ensure execution fails gracefully.
                if (!TryParseTemplateArgument(templateArgument, out result)) {
                    return;
                }

                if (result < min) {
                    min = result;
                }

                if (result > max) {
                    max = result;
                }
            }

            // If the template arguments do not start at 0,
            // or do not match the correct total count,
            // report a diagnostic.
            if (min != 0 || max != count - 1) {
                ReportDiagnostic(
                    context,
                    Descriptor,
                    node.ArgumentList.Arguments[1].GetLocation(),
                    node);
            }
        }

        /// <summary>
        /// Attempts to parse a string interpolation template argument to its integer value.
        /// </summary>
        /// <param name="templateArgument">The template argument.</param>
        /// <param name="result">The result.</param>
        private static bool TryParseTemplateArgument(string templateArgument, out int result) {
            int start = -1;

            for (int i = 0; i < templateArgument.Length; ++i) {
                if (templateArgument[i] == '{') {
                    start = i + 1;
                }
            }

            return int.TryParse(templateArgument.Substring(start, templateArgument.Length - start), out result);
        }

        #endregion Methods
    }
}
