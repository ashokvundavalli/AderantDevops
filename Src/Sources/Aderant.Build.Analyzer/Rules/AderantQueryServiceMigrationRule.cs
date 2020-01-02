using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {

    public class AderantQueryServiceMigrationRule : RuleBase {
        internal const string DiagnosticId = "Aderant_NewQueryServiceCallStyle";

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Warning;
        internal override string Id => DiagnosticId;
        
        internal override string Title => "Help move query service calls from old style to new style";
        internal override string MessageFormat => "Query Service call error";
        internal override string Description => "Query Service calls should be in the new format.";

        public override DiagnosticDescriptor Descriptor => new DiagnosticDescriptor(
            Id,
            Title,
            MessageFormat,
            AnalyzerCategory.QueryService2,
            Severity,
            true,
            Description);

        public override void Initialize(AnalysisContext context) {
            //context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
            context.RegisterSyntaxNodeAction(AnalyzeIdentifierAccess, SyntaxKind.IdentifierName);
            context.RegisterSyntaxNodeAction(AnalyzeMethodCall, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeMethodCall(SyntaxNodeAnalysisContext context) {
            var invocationNode = context.Node as InvocationExpressionSyntax;
            var typeInfo = context.SemanticModel.GetTypeInfo(invocationNode);
            if (invocationNode != null && (typeInfo.Type.Name == "IQueryServiceProxy" || typeInfo.Type.Name == "QueryServiceProxy")) {
                var accessedItemNode = invocationNode.GetNodeAfterMe();
                if (accessedItemNode != null && !IsCorrectQueryServiceCall(accessedItemNode)) {
                    ReportDiagnostic(context, Descriptor, accessedItemNode.GetLocation(), accessedItemNode);
                }
            }
        }

        private void AnalyzeIdentifierAccess(SyntaxNodeAnalysisContext context) {
            var invocationNode = context.Node as IdentifierNameSyntax;
            var typeInfo = context.SemanticModel.GetTypeInfo(invocationNode);
            if (invocationNode != null && invocationNode.Parent.GetType().Name == "MemberAccessExpressionSyntax" && typeInfo.Type != null && (typeInfo.Type.Name == "IQueryServiceProxy" || typeInfo.Type.Name == "QueryServiceProxy")) {
                var accessedItemNode = invocationNode.GetNodeAfterMe();
                if (accessedItemNode != null && !IsCorrectQueryServiceCall(accessedItemNode)) {
                    ReportDiagnostic(context, Descriptor, accessedItemNode.GetLocation(), accessedItemNode);
                }
            }
        }

        private bool IsCorrectQueryServiceCall(SyntaxNode node) {
            var identifierNode = node as IdentifierNameSyntax;
            string text;
            if (identifierNode == null) {
                var genericNameNode = node as GenericNameSyntax;
                if (genericNameNode == null) {
                    return false;
                }
                text = genericNameNode.Identifier.Text;

            } else {
                text = identifierNode.Identifier.Text;
            }
            string[] queryMethods = { "Query", "Methods", "ExecuteBatchQuery", "IgnoreResourceNotFoundException" };
            if (queryMethods.Any(m => m == text)) {
                return true;
            }

            if (text.StartsWith("Get", System.StringComparison.Ordinal)) { // leave get things alone, they need to be changed to procs etc. compile failure can catch this.
                return true;
            }
            return false;
        }
    }
}
