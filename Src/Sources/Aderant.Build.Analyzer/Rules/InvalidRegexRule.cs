using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {

    public class InvalidRegexRule : RuleBase {
        internal const string DiagnosticId = "Aderant_InvalidRegex";

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        internal override string Id => DiagnosticId;
        
        internal override string Title => "Regex error parsing string argument";
        internal override string MessageFormat => "Regex error '{0}'";
        internal override string Description => "Regex patterns should be syntactically valid.";

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

            string regex;
            LiteralExpressionSyntax regexLiteral;
            if (!TryGetRegex(invocationExpression, context.SemanticModel, out regex, out regexLiteral)) {
                return;
            }

            // Test if the provided regex is a valid one.
            try {
                System.Text.RegularExpressions.Regex.Match(string.Empty, regex);
            } catch (ArgumentException ex) {

                // If the regex is invalid, produce a diagnostic.
                var diagnostic = Diagnostic.Create(Descriptor, invocationExpression.GetLocation(), ex.Message);

                context.ReportDiagnostic(diagnostic);
            }
        }

        internal static bool TryGetRegex(
            InvocationExpressionSyntax invocationExpression, 
            SemanticModel semanticModel, 
            out string regex, 
            out LiteralExpressionSyntax regexLiteral) {

            regex = null;
            regexLiteral = null;

            // Check if this is a call to a method named Match as in Regex.Match.
            var memberAccessExpression = invocationExpression.Expression as MemberAccessExpressionSyntax;
            if (memberAccessExpression?.Name.ToString() != "Match") {
                return false;
            }

            // Check if the actual System.Text.RegularExpression.Regex.Match method is being called.
            var memberSymbol = semanticModel.GetSymbolInfo(memberAccessExpression).Symbol as IMethodSymbol;
            if (!memberSymbol?.ToString().StartsWith("System.Text.RegularExpressions.Regex.Match") ?? true) {
                return false;
            }

            // Check if there are at least 2 arguments provided.
            ArgumentListSyntax argumentList = invocationExpression.ArgumentList;
            if ((argumentList?.Arguments.Count ?? 0) < 2) {
                return false;
            }

            // Check if the second argument is a string literal which we can further examine.
            regexLiteral = argumentList?.Arguments[1].Expression as LiteralExpressionSyntax;
            if (regexLiteral == null) {
                return false;
            }
            var regexOptional = semanticModel.GetConstantValue(regexLiteral);
            if (!regexOptional.HasValue) {
                return false;
            }
            regex = regexOptional.Value as string;
            if (regex == null) {
                return false;
            }

            return true;
        }
    }
}
