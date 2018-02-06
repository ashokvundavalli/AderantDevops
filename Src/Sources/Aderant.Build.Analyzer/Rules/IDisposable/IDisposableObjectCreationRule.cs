using System;
using System.Linq;
using Aderant.Build.Analyzer.Extensions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.IDisposable {
    internal class IDisposableObjectCreationRule : IDisposableRuleBase {
        #region Properties

        internal override string Title => "Aderant IDisposable Invocation Diagnostic";

        internal override string MessageFormat => "Object of type '{0}' implementing 'System.IDisposable' " +
                                                  "is created but is not disposed.";

        internal override string Description => "Ensure the object is correctly disposed.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(ProcessNode, SyntaxKind.ObjectCreationExpression);
        }

        /// <summary>
        /// Processes the method invocation node.
        /// </summary>
        /// <param name="context">The context.</param>
        private void ProcessNode(SyntaxNodeAnalysisContext context) {
            var node = context.Node as ObjectCreationExpressionSyntax;

            // Exit early if execution is not processing an object creation expression,
            //      or if analysis is suppressed.
            //      or if the node is not Disposable.
            //      or if the node's parent is a using statement, or assignment expression.
            if (node == null ||
                IsAnalysisSuppressed(node, ValidSuppressionMessages) ||
                !GetIsNodeDisposable(node, context.SemanticModel) ||
                node.Parent is UsingStatementSyntax ||
                node.Parent is AssignmentExpressionSyntax ||
                node.Parent is EqualsValueClauseSyntax) {
                // ...exit.
                return;
            }

            // If the node's parent is a return statement, and is not contained within a property accessor...
            if (node.Parent is ReturnStatementSyntax &&
                node.Parent.GetAncestorOfType<AccessorDeclarationSyntax>() == null) {
                // ...exit.
                return;
            }

            // If the node is a child of an array creation expression...
            if (node.GetAncestorOfType<ImplicitArrayCreationExpressionSyntax>() != null ||
                node.GetAncestorOfType<ArrayCreationExpressionSyntax>() != null) {
                // ...exit.
                return;
            }

            // Conditional expressions.
            // condition ? true : false
            var currentNode = node.Parent;

            while (currentNode is ConditionalExpressionSyntax) {
                currentNode = currentNode.Parent;
            }

            if (currentNode is ReturnStatementSyntax &&
                currentNode.GetAncestorOfType<AccessorDeclarationSyntax>() == null) {
                return;
            }

            // Coalesce expressions.
            // item ?? new Item();
            var binaryExpressionSyntax = node.Parent as BinaryExpressionSyntax;

            if (binaryExpressionSyntax?.Kind() == SyntaxKind.CoalesceExpression &&
                binaryExpressionSyntax.Parent is AssignmentExpressionSyntax) {
                return;
            }

            // Parameters
            var argumentSyntax = node.Parent as ArgumentSyntax;
            var argumentListSyntax = argumentSyntax?.Parent as ArgumentListSyntax;

            // This constructor.
            // this.Class(new Item());
            var constructorInitializerSyntax = argumentListSyntax?.Parent as ConstructorInitializerSyntax;

            if (constructorInitializerSyntax?.Kind() == SyntaxKind.ThisConstructorInitializer) {
                return;
            }

            // Collections
            // List.Add(new Item());
            var invocationExpressionSyntax = argumentListSyntax?.Parent as InvocationExpressionSyntax;
            var memberAccessExpressionSyntax = invocationExpressionSyntax?.Expression as MemberAccessExpressionSyntax;

            if (memberAccessExpressionSyntax != null) {
                string methodName = memberAccessExpressionSyntax
                    .Name
                    .Identifier
                    .Text;

                var interfaces = context
                    .SemanticModel
                    .GetSymbolInfo(invocationExpressionSyntax)
                    .Symbol
                    .ContainingType
                    .AllInterfaces;

                if (string.Equals(methodName, "Add", StringComparison.Ordinal) &&
                    interfaces.Any(
                        x => string.Equals(
                            "System.Collections.Generic.IEnumerable<T>",
                            x.OriginalDefinition.ToDisplayString(),
                            StringComparison.Ordinal))) {
                    return;
                }
            }

            // If execution reaches this point, this use case is illegal.
            ReportDiagnostic(context, Descriptor, node.GetLocation(), node, node.Type.ToString());
        }

        #endregion Methods
    }
}
