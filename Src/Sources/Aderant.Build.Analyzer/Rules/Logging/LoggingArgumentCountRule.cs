using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.Logging {
    public class LoggingArgumentCountRule : LoggingRuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_Logging_ArgumentCount";

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

        internal override string Title => "Invalid Argument Count";

        internal override string MessageFormat => Description;

        internal override string Description => "Log message interpolation argument count mismatch. " +
                                                "Template expected '{0}' arguments, but " +
                                                "'{1}' arguments were provided.";

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

            // Get the number of parameters provided as the 'params object[]' argument,
            // ignoring the exception object, if one is provided.
            int actualArgumentCount = GetLogMethodParametersCount(context.SemanticModel, node);

            // Argument [0] is the LogLevel, thus argument [1] is the string template.
            var result = GetInterpolationTemplateArguments(node.ArgumentList.Arguments[1]);

            if (result == null) {
                return;
            }

            int expectedArgumentCount = result.Count();

            // Raise a diagnostic if the expected argument count does not match the actual count.
            if (expectedArgumentCount != actualArgumentCount) {
                ReportDiagnostic(
                    context,
                    Descriptor,
                    node.GetLocation(),
                    node,
                    expectedArgumentCount,
                    actualArgumentCount);
            }
        }

        #endregion Methods
    }
}
