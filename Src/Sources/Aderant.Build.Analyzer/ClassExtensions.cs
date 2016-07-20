using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer {
    public static class ClassExtensions {

        /// <summary>
        /// Determines whether the property name is found on this instance.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="propertyName">Name of the property.</param>
        /// <returns></returns>
        public static bool IsMemberOnClassParentNode(this SyntaxNodeAnalysisContext context, string propertyName) {
            var node = context.Node;
            while (node != null && !(node is ClassDeclarationSyntax)) {
                node = node.Parent;
            }
            if (node != null)
            {
                var classDeclarationExpression = node as ClassDeclarationSyntax;
                INamedTypeSymbol namedTypeSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclarationExpression) as INamedTypeSymbol;
                if (HasMember(namedTypeSymbol, propertyName)) {
                    return true;
                }
            }
            return false;
        }

        private static bool HasMember(INamedTypeSymbol namedTypeSymbol, string propertyName) {
            var members = namedTypeSymbol.MemberNames.ToList();
            if (!members.Contains(propertyName)) {
                if (namedTypeSymbol.BaseType != null) {
                    return HasMember(namedTypeSymbol.BaseType, propertyName);
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// Gets the next child node after me.
        /// </summary>
        /// <param name="me">Me.</param>
        /// <returns></returns>
        public static SyntaxNode GetNodeAfterMe(this SyntaxNode me) {
            SyntaxNode peerNode = null;
            var peerNodes = me.Parent?.ChildNodes().ToArray();
            for (int i = 0; i < peerNodes.Length; i++) {
                if (peerNodes[i].IsEquivalentTo(me) && i < peerNodes.Length - 1) {
                    peerNode = peerNodes[i + 1];
                    break;
                }
            }
            return peerNode;
        }
    }
}
