using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Aderant.Build.Analyzer.Extensions;
using Aderant.Build.Analyzer.Lists.IDisposable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.IDisposable {
    internal class IDisposableMethodInvocationRule : IDisposableRuleBase {
        #region Properties

        internal override string Title => "Aderant IDisposable Invocation Diagnostic";

        internal override string MessageFormat => "Type implementing 'System.IDisposable' " +
                                                  "is returned from invocation but is not disposed.";

        internal override string Description => "Ensure the object is correctly disposed.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(ProcessNode, SyntaxKind.InvocationExpression);
        }

        /// <summary>
        /// Processes the method invocation node.
        /// </summary>
        /// <param name="context">The context.</param>
        private void ProcessNode(SyntaxNodeAnalysisContext context) {
            var node = context.Node as InvocationExpressionSyntax;

            // Exit early if execution is not processing an invocation expression,
            //       or if the node is a direct child of a using statement.
            //       or if analysis is suppressed,
            //       or if the invocation expression's direct parent is a using statement.
            if (node == null ||
                node.Parent is UsingStatementSyntax ||
                IsAnalysisSuppressed(node, ValidSuppressionMessages) ||
                GetIsWhitelisted(node, context.SemanticModel)) {
                return;
            }

            // Determine if the invocation is a 'Remove' method invoked upon a collection of IDisposable items.
            if (EvaluateRemoveMethod(node, context.SemanticModel)) {
                // Report the diagnostic.
                ReportDiagnostic(
                    context,
                    Descriptor,
                    GetDiagnosticLocation(node),
                    node);
            }

            // Exit early if the method's return value does not inherit from the System.IDisposable interface.
            if (!GetIsNodeDisposable(node, context.SemanticModel)) {
                return;
            }

            // Exit early if the method is an extension method.
            if (GetIsNodeExtensionMethod(node, context.SemanticModel)) {
                return;
            }

            // Attempt to interpret the parent expression as a chained invocation expression.
            // Example:
            //       someObject.SomeMethod().Dispose();
            // If any of the text values are 'Dispose'...
            var parentExpression = node.Parent as MemberAccessExpressionSyntax;

            if (parentExpression != null &&
                parentExpression
                    .ChildNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Select(nameSyntax => nameSyntax.Identifier.Text)
                    .Any(
                        text =>
                            text.Equals("Dispose", StringComparison.Ordinal) ||
                            text.Equals("Close", StringComparison.Ordinal))) {
                // ...the Dispose method was invoked as part of the chain, and execution may exit without error.
                return;
            }

            // Takes the current invocation expression and retrieves the parent expression.
            // Uses the parent expression to find all child invocation expressions.
            // Example:
            //      SomeExpression().SomeOtherExpression()
            // The above example would return both 'chained' invocation expressions.
            // From each chained expression, retrieve the actual expression,
            //      rather than the invocation 'wrapper' expression.
            // From the resulting expressions, retrieve all child expressions that are identifiers.
            // From the identifiers, select the identifier's text value.
            // If any of the text values are 'Dispose'...
            if (node
                .Parent
                .ChildNodes()
                .OfType<InvocationExpressionSyntax>()
                .Select(x => x.Expression)
                .Select(
                    expression => expression.ChildNodes()
                        .OfType<IdentifierNameSyntax>()
                        .Select(x => x.Identifier.Text))
                .Any(
                    identifierNames => identifierNames.Any(
                        text =>
                            text.Equals("Dispose", StringComparison.Ordinal) ||
                            text.Equals("Close", StringComparison.Ordinal)))) {
                // ...the Dispose method was invoked as part of the chain, and execution may exit without error.
                return;
            }

            // Process the current node's ancestors, exiting if this node is not a rule violation.
            if (ProcessAncestorNodes(node.Ancestors().ToList(), context.SemanticModel)) {
                return;
            }

            // Process the current node as a workflow dependency invocation,
            // exiting if the node is a dependency invocation.
            if (ProcessWorkflowDependency(node, context.SemanticModel)) {
                return;
            }

            // Report the diagnostic.
            ReportDiagnostic(
                context,
                Descriptor,
                GetDiagnosticLocation(node),
                node);
        }

        /// <summary>
        /// Evaluates usages of 'Remove' methods operating on collections containing disposable objects.
        /// </summary>
        /// <param name="invocationExpression">The invocation expression.</param>
        /// <param name="semanticModel">The semantic model.</param>
        private static bool EvaluateRemoveMethod(
            InvocationExpressionSyntax invocationExpression,
            SemanticModel semanticModel) {
            // Get the 'Original Definition' of the method being invoked.
            // Example: List<T>.RemoveAt(int), rather than List<SomeObject>.RemoveAt(int)
            string originalDefinitionString = semanticModel
                .GetSymbolInfo(invocationExpression)
                .Symbol?
                .OriginalDefinition
                .ToDisplayString();

            // If for some unfathomable reason there is no original definition...
            if (string.IsNullOrWhiteSpace(originalDefinitionString)) {
                // ...a 'Remove' method is not being invoked.
                return false;
            }

            bool? isList = null;

            // Set a flag indicating the type of collection being operated upon:
            // List     - List<T>
            // NotList  - Dictionary<TKey, TValue>
            // Null     - No collection.
            if (string.Equals(
                    originalDefinitionString,
                    "System.Collections.Generic.List<T>.RemoveAll(System.Predicate<T>)",
                    StringComparison.Ordinal) ||
                string.Equals(
                    originalDefinitionString,
                    "System.Collections.Generic.List<T>.RemoveAt(int)",
                    StringComparison.Ordinal) ||
                string.Equals(
                    originalDefinitionString,
                    "System.Collections.Generic.List<T>.RemoveRange(int, int)",
                    StringComparison.Ordinal)) {
                isList = true;
            } else if (string.Equals(
                originalDefinitionString,
                "System.Collections.Generic.Dictionary<TKey, TValue>.Remove(TKey)",
                StringComparison.Ordinal)) {
                isList = false;
            }

            // If no collection is being operated upon...
            if (!isList.HasValue) {
                // ...a 'Remove' method is not being invoked.
                return false;
            }

            // Get the identifier being operated upon.
            // Example: the 'someList'
            //      someList.RemoveAt(0);
            var identifierNameSyntax = invocationExpression
                .Expression
                .ChildNodes()
                .OfType<IdentifierNameSyntax>()
                .FirstOrDefault();

            // If the method is not being invoked upon an object...
            if (identifierNameSyntax == null) {
                // ...there are bigger problems afoot.
                return false;
            }

            // Get the type of the identifier being operated upon.
            var typeSymbol = semanticModel.GetTypeInfo(identifierNameSyntax).Type as INamedTypeSymbol;

            // If for some unfathomable reason the object doesn't have a type,
            // or if the object does not have any type arguments...
            if (typeSymbol == null || typeSymbol.TypeArguments.IsEmpty) {
                // ...the invocation is not a 'Remove' method that is of concern.
                return false;
            }

            // If the collection type is a list...
            if (isList == true) {
                // ...return whether the 'T' in List<T> is IDisposable.
                return GetIsDisposable(typeSymbol.TypeArguments[0]);
            }

            // If the collection type is not a list (and therefore is a dictionary),
            // return whether the dictionary's value type is IDisposable.
            return typeSymbol.TypeArguments.Length == 2 &&
                   GetIsDisposable(typeSymbol.TypeArguments[1]);
        }

        /// <summary>
        /// Gets the location of the diagnostic.
        /// </summary>
        /// <param name="node">The node.</param>
        private static Location GetDiagnosticLocation(InvocationExpressionSyntax node) {
            if (node == null) {
                return null;
            }

            if (node.Expression == null) {
                return node.GetLocation();
            }

            var childNodes = node.Expression.ChildNodes()?.ToList();

            if (childNodes == null || childNodes.Count < 2) {
                return node.GetLocation();
            }

            return childNodes[1] != null
                ? childNodes[1].GetLocation()
                : node.GetLocation();
        }

        /// <summary>
        /// Determines if the node is an extension method.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="semanticModel">The semantic model.</param>
        private static bool GetIsNodeExtensionMethod(
            ExpressionSyntax node,
            SemanticModel semanticModel) {
            return (semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol)?.IsExtensionMethod == true;
        }

        /// <summary>
        /// Determines whether the specified node is whitelisted.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="semanticModel">The semantic model.</param>
        private static bool GetIsWhitelisted(ExpressionSyntax node, SemanticModel semanticModel) {
            string methodDisplayString = semanticModel
                .GetSymbolInfo(node)
                .Symbol?
                .OriginalDefinition
                .ToDisplayString();

            return !string.IsNullOrWhiteSpace(methodDisplayString) &&
                   IDisposableWhitelist
                       .Methods
                       .Any(signature => signature.Equals(methodDisplayString, StringComparison.Ordinal));
        }

        /// <summary>
        /// Processes the ancestor nodes.
        /// </summary>
        /// <param name="ancestors">The ancestors.</param>
        /// <param name="semanticModel">The semantic model.</param>
        private static bool ProcessAncestorNodes(
            IReadOnlyCollection<SyntaxNode> ancestors,
            SemanticModel semanticModel) {
            // Iterate through the node's ancestors.
            foreach (var ancestorNode in ancestors) {
                // If the ancestor is a method invocation expression,
                // wrapping a valid member access expression...
                var expression = (ancestorNode as InvocationExpressionSyntax)?.Expression as MemberAccessExpressionSyntax;

                if (expression == null) {
                    continue;
                }

                // ...and the method invoked is whitelisted...
                if (GetIsWhitelisted(expression, semanticModel)) {
                    // ...return without error.
                    return true;
                }
            }

            // Iterate through the node's ancestors.
            foreach (var ancestorNode in ancestors) {
                // If the node is a method argument...
                if (ancestorNode is ArgumentSyntax) {
                    // ...break early and error.
                    break;
                }

                // If the node is a return statement, or assignment expression...
                if (ancestorNode is ReturnStatementSyntax ||
                    ancestorNode is EqualsValueClauseSyntax ||
                    ancestorNode is AssignmentExpressionSyntax) {
                    // ...return without error, as this is handled by the local and field rules.
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Processes the workflow dependency.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="semanticModel">The semantic model.</param>
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration", Justification = "ReSharper bug.")]
        private static bool ProcessWorkflowDependency(
            SyntaxNode node,
            SemanticModel semanticModel) {
            // Get the parented invocation expression,
            // only if the current invocation expression is an argument to the parent.
            var invocation = node
                .GetAncestorOfType<ArgumentSyntax>()?
                .GetAncestorOfType<InvocationExpressionSyntax>();

            // Get the child nodes from the parent invocation expression's member access expression.
            var childNodes = invocation?
                .Expression?
                .ChildNodes()?
                .OfType<IdentifierNameSyntax>()
                .ToList();

            // Two child nodes are expected:
            // 1. The object being acted upon
            // 2. The action
            if (childNodes?.Count != 2) {
                return false;
            }

            // Get the symbol of the object being acted upon.
            var parameterSymbol = semanticModel.GetSymbolInfo(childNodes[0]).Symbol as IParameterSymbol;

            // While loop to iterate through base types of symbol.
            bool isActivityContext = false;
            var typeSymbol = parameterSymbol?.Type;
            while (typeSymbol != null) {
                // If the current type is an ActivityContext, set appropriate flag and exit loop.
                if (string.Equals(
                    "System.Activities.ActivityContext",
                    typeSymbol.OriginalDefinition.ToDisplayString(),
                    StringComparison.Ordinal)) {
                    isActivityContext = true;
                    typeSymbol = null;
                } else {
                    // Otherwise, set the current object to be the current object's base type.
                    typeSymbol = typeSymbol.BaseType;
                }
            }

            // If the invocation is the 'GetDependency' method invoked on an ActivityContext object, return true.
            return isActivityContext &&
                   string.Equals("GetDependency", childNodes[1].Identifier.Text, StringComparison.Ordinal);
        }

        #endregion Methods
    }
}
