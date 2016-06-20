using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {

    public class SetPropertyValueNoStringNonFixableRule : RuleBase {
        internal const string DiagnosticId = "Aderant_SetPropertyValueNoStringNonFixable";

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        internal override string Id => "Aderant_SetPropertyValueNoStringNonFixable";

        internal override string Title => "Invalid string usage in SetPropertyValue";
        internal override string MessageFormat => "SetPropertyValue uses string '{0}' which is not a property found on this type";
        internal override string Description => "Use nameof() to gain type-safety, refactoring benefits and avoid errors like this.";

        public override DiagnosticDescriptor Descriptor => new DiagnosticDescriptor(
           id: Id,
           title: Title,
           messageFormat: MessageFormat,
           category: AnalyzerCategory.Syntax,
           defaultSeverity: Severity,
           isEnabledByDefault: true,
           description: Description);

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context) {

            var invocationExpression = (InvocationExpressionSyntax)context.Node;

            string propertyName;
            LiteralExpressionSyntax propertyNameLiteral;
            if (!SetPropertyValueNoStringRule.TryGetPropertyName(invocationExpression, context.SemanticModel, out propertyName, out propertyNameLiteral)) {
                return;
            }

            // Test if the property name is found on this instance.
            if (!context.IsMemberOnClassParentNode(propertyName)) {

                // If the property name is not found on this instance, produce a diagnostic.
                var diagnostic = Diagnostic.Create(Descriptor, invocationExpression.GetLocation(), propertyName);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
