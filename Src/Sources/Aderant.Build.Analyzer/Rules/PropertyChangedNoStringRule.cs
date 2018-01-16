using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {

    public class PropertyChangedNoStringRule : RuleBase {
        internal const string DiagnosticId = "Aderant_PropertyChangedNoString";

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Warning;
        internal override string Id => DiagnosticId;

        internal override string Title => "String usage in PropertyChanged";
        internal override string MessageFormat => "PropertyChanged uses string '{0}' instead of nameof({0})";
        internal override string Description => "Use nameof() to gain type-safety and refactoring benefits.";

        public override DiagnosticDescriptor Descriptor => new DiagnosticDescriptor(
            Id,
            Title,
            MessageFormat,
            AnalyzerCategory.Syntax,
            Severity,
            true,
            Description);

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeNode(SyntaxNodeAnalysisContext context) {

            var invocationExpression = (InvocationExpressionSyntax)context.Node;

            string propertyName;
            LiteralExpressionSyntax propertyNameLiteral;
            if (!TryGetPropertyName(invocationExpression, context.SemanticModel, out propertyName, out propertyNameLiteral)) {
                return;
            }

            // Test if the property name is found on this instance.
            if (!context.IsMemberOnClassParentNode(propertyName)) {
                return;
            }

            // If the property name is found on this instance, produce a diagnostic.
            ReportDiagnostic(context, Descriptor, invocationExpression.GetLocation(), invocationExpression, propertyName);
        }

        internal static bool TryGetPropertyName(
            InvocationExpressionSyntax invocationExpression,
            SemanticModel semanticModel,
            out string propertyName,
            out LiteralExpressionSyntax propertyNameLiteral) {

            propertyName = null;
            propertyNameLiteral = null;

            // Check if the a method OnPropertyChanged, SendPropertyChanged, NotifyPropertyChanged or RaisePropertyChanged is being called.
            var memberSymbol = semanticModel.GetSymbolInfo(invocationExpression).Symbol as IMethodSymbol;
            if (!memberSymbol?.Name.EndsWith("PropertyChanged") ?? true) {
                return false;
            }

            // Check if there is exactly 1 argument provided.
            ArgumentListSyntax argumentList = invocationExpression.ArgumentList;
            if ((argumentList?.Arguments.Count ?? 0) != 1) {
                return false;
            }

            // Check if the argument is a string literal which we can further examine.
            propertyNameLiteral = argumentList?.Arguments[0].Expression as LiteralExpressionSyntax;
            if (propertyNameLiteral == null) {
                return false;
            }
            var propertyNameOptional = semanticModel.GetConstantValue(propertyNameLiteral);
            if (!propertyNameOptional.HasValue) {
                return false;
            }
            propertyName = propertyNameOptional.Value as string;
            if (propertyName == null) {
                return false;
            }

            return true;
        }
    }
}
