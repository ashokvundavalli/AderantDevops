using System;
using System.Linq;
using Aderant.Build.Analyzer.Extensions;
using Microsoft.CodeAnalysis;
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

            if (ProcessReturnStatements(node, context.SemanticModel)) {
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
                        interfaceSymbol => string.Equals(
                            "System.Collections.Generic.IEnumerable<T>",
                            interfaceSymbol.OriginalDefinition.ToDisplayString(),
                            StringComparison.Ordinal))) {
                    return;
                }

                if (string.Equals(methodName, "Enqueue", StringComparison.Ordinal) &&
                    interfaces.Any(
                        interfaceSymbol => string.Equals(
                            "System.Collections.Generic.IEnumerable<T>",
                            interfaceSymbol.OriginalDefinition.ToDisplayString(),
                            StringComparison.Ordinal))) {
                    return;
                }
            }

            // If execution reaches this point, this use case is illegal.
            ReportDiagnostic(context, Descriptor, node.GetLocation(), node, node.Type.ToString());
        }

        /// <summary>
        /// Processes the return statements.
        /// If the object being created is a child to a return statement,
        /// ignore the object creation if the creation satisfies certian conditions.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <returns>
        /// True if execution of the node can halt.
        /// False if execution is to continue.
        /// </returns>
        private static bool ProcessReturnStatements(SyntaxNode node, SemanticModel semanticModel) {
            // Get the return node.
            var returnNode = node.GetAncestorOfType<ReturnStatementSyntax>() ??
                             node.GetAncestorOfType<YieldStatementSyntax>() as StatementSyntax;

            if (returnNode == null) {
                return false;
            }

            var methodDeclaration = returnNode.GetAncestorOfType<MethodDeclarationSyntax>();

            // Objects that are created and returned outside of a method
            // are ignored if they are outside of a property accessor.
            if (methodDeclaration == null) {
                return returnNode.GetAncestorOfType<AccessorDeclarationSyntax>() == null;
            }

            // Object creations within methods are ignored if the return type is IDisposable.
            // As this ensures that no matter what is returned, the resulting object will be disposed.
            var symbol = semanticModel.GetTypeInfo(methodDeclaration.ReturnType).Type;

            if (GetIsDisposable(symbol)) {
                return false;
            }

            var namedSymbol = symbol as INamedTypeSymbol;

            if (namedSymbol == null) {
                return false;
            }

            if (!namedSymbol.IsGenericType) {
                return false;
            }

            INamedTypeSymbol returnSymbol;

            // Handling for dictionaries vs standard collections.
            if (string.Equals(
                namedSymbol.OriginalDefinition.ToDisplayString(),
                "System.Collections.Generic.Dictionary<TKey, TValue>",
                StringComparison.Ordinal)) {
                if (namedSymbol.TypeArguments.Length != 2) {
                    return false;
                }

                returnSymbol = namedSymbol.TypeArguments[1] as INamedTypeSymbol;
            } else {
                if (namedSymbol.TypeArguments.Length != 1) {
                    return false;
                }

                returnSymbol = namedSymbol.TypeArguments[0] as INamedTypeSymbol;
            }

            return GetIsDisposable(returnSymbol);
        }

        #endregion Methods
    }
}
