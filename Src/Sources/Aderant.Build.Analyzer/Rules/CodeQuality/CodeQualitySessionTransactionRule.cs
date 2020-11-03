using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.CodeQuality {
    public sealed class CodeQualitySessionTransactionRule : RuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_CodeQuality_SessionTransaction";

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

        internal override string Title => "'Session.Transaction' Error";

        internal override string MessageFormat => Description;

        internal override string Description => "GetSession().BeginTransaction() chains do not dispose of the created session object.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNodeSimpleMemberAccessExpression, SyntaxKind.SimpleMemberAccessExpression);
        }

        private void AnalyzeNodeSimpleMemberAccessExpression(SyntaxNodeAnalysisContext context) {
            var node = context.Node as MemberAccessExpressionSyntax;

            if (node == null ||
                !string.Equals("BeginTransaction", node.Name.Identifier.Text, StringComparison.Ordinal) ||
                IsAnalysisSuppressed(node, DiagnosticId)) {
                return;
            }

            SyntaxNode[] memberAccessExpressionSyntaxNodes;
            if (!TryGetMemberAccessExpressionSyntaxes(node, out memberAccessExpressionSyntaxNodes)) {
                return;
            }

            IdentifierNameSyntax contextNameSyntax;
            if (!TryGetContextNameSyntax(
                memberAccessExpressionSyntaxNodes,
                context,
                out contextNameSyntax)) {
                return;
            }

            ReportDiagnostic(
                context,
                Descriptor,
                contextNameSyntax.GetLocation(),
                contextNameSyntax);
        }

        /// <summary>
        /// Tries to get the member access expression syntax.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="memberAccessExpressionSyntaxNodes">The member access expression syntax nodes.</param>
        private static bool TryGetMemberAccessExpressionSyntaxes(
            SyntaxNode node,
            out SyntaxNode[] memberAccessExpressionSyntaxNodes) {
            memberAccessExpressionSyntaxNodes = null;

            // Recursively get all the MemberAccessExpressionSyntax nodes from the current node's children.
            // As the node tree is constructed in reverse for method chains, this will include the entire chain.
            var memberAccessExpressions = new List<MemberAccessExpressionSyntax>(2);
            GetExpressionsFromChildNodes(ref memberAccessExpressions, node);

            // The method chain is expected to be at least two methods deep.
            if (memberAccessExpressions.Count < 2) {
                return false;
            }

            // The two methods are expected to get 'GetSession' and 'Repository'.
            // As MemberAccessExpressionSyntax is used, accessing the Repository property also appears in the list of child nodes.
            if (!string.Equals("GetSession", memberAccessExpressions[0].Name.Identifier.Text, StringComparison.Ordinal) ||
                !string.Equals("Repository", memberAccessExpressions[1].Name.Identifier.Text, StringComparison.Ordinal)) {
                return false;
            }

            // Simplify the array, as there may be addition invocations in the chain.
            memberAccessExpressionSyntaxNodes = new SyntaxNode[] {
                memberAccessExpressions[0],
                memberAccessExpressions[1]
            };

            return true;
        }

        /// <summary>
        /// Tries to get the context name syntax.
        /// </summary>
        /// <param name="nodes">The nodes.</param>
        /// <param name="context">The context.</param>
        /// <param name="contextNameSyntax">The context name syntax.</param>
        private static bool TryGetContextNameSyntax(
            IReadOnlyList<SyntaxNode> nodes,
            SyntaxNodeAnalysisContext context,
            out IdentifierNameSyntax contextNameSyntax) {
            contextNameSyntax = null;

            // Example expression:
            //      context.Repository.GetSession().BeginTransaction()
            // 'nodes' will contain two expressions:
            //      [0]: GetSession
            //      [1]: Repository

            // Get the child nodes of the 'Repository' expression.
            var childNodes = nodes[1].ChildNodes().ToList();

            // At least one child node is required.
            if (childNodes.Count < 1) {
                return false;
            }

            // The first child node is required to be an identifier name for the 'context' variable.
            var nameSyntax = childNodes[0] as IdentifierNameSyntax;

            // Child node is an invalid type.
            if (nameSyntax == null) {
                return false;
            }

            // Get the fully qualified path for the context variable's type.
            var originalDefinition = context
                .SemanticModel
                .GetSymbolInfo(nameSyntax, context.CancellationToken)
                .Symbol?
                .OriginalDefinition
                .ToString();

            // Confirm the context is of the correct type.
            // If true, then the types of the rest of the expression chain are also known.
            if (!string.Equals("Aderant.Framework.Communication.CallContext", originalDefinition, StringComparison.Ordinal)) {
                return false;
            }

            // Get the child nodes of the 'GetSession' expression.
            childNodes = nodes[0].ChildNodes().ToList();

            // At least two child nodes are expected, with the first being the continuation of the invocation chain.
            if (childNodes.Count < 2) {
                return false;
            }

            // The second child node will be the method name identifier: GetSession
            nameSyntax = childNodes[1] as IdentifierNameSyntax;

            // The second child is not an identifier type.
            if (nameSyntax == null) {
                return false;
            }

            contextNameSyntax = nameSyntax;
            return true;
        }

        #endregion Methods
    }
}
