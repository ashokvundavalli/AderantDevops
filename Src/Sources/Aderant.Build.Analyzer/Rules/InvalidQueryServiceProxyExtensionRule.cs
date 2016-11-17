using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {

    public class InvalidQueryServiceProxyExtensionRule : RuleBase {
        internal const string DiagnosticId = "Aderant_InvalidQueryServiceProxyExtension";

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Error;
        internal override string Id => DiagnosticId;
        
        internal override string Title => "Invalid extension";
        internal override string MessageFormat => "{0} cannot be used with arguments as an extension method for IQueryServiceProxy because OData doesn't support this.";
        internal override string Description => "Invalid query service proxy extension method used.";


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
            string extensionName;

            if (!UsesInvalidExtension(invocationExpression, context.SemanticModel, out extensionName)) {
                return;
            }

            var diagnostic = Diagnostic.Create(Descriptor, invocationExpression.GetLocation(), extensionName);
            context.ReportDiagnostic(diagnostic);
        }

        internal static bool UsesInvalidExtension(
            InvocationExpressionSyntax invocationExpression, 
            SemanticModel semanticModel,
            out string extensionName) {

            // check if this is a call to an extension method that is not allowed together with arguments
            var memberAccessExpression = invocationExpression.Expression as MemberAccessExpressionSyntax;
            extensionName = memberAccessExpression?.Name.ToString();
            if (extensionName != null && (!extensionName.StartsWith("First") && !extensionName.StartsWith("Single") && extensionName != "Count")) {
                return false;
            }

            // check if the extension method has arguments which would not be allowed
            if (!invocationExpression.ArgumentList.Arguments.Any()) {
                return false;
            }

            var innerMemberAccessExpression = memberAccessExpression?.Expression as MemberAccessExpressionSyntax;


            if (innerMemberAccessExpression != null) {
                var symbolInfo = semanticModel.GetSymbolInfo(innerMemberAccessExpression);
                if (symbolInfo.Symbol != null && (symbolInfo.Symbol.ContainingType.Name == "IQueryServiceProxy" || symbolInfo.Symbol.ContainingType.Interfaces.Any(i => i.Name == "IQueryServiceProxy"))) {
                    return true;
                }
            }

            return false;
        }
    }
}
