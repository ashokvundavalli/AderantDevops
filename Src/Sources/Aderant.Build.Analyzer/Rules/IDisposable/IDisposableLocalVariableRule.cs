using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.IDisposable {
    internal class IDisposableLocalVariableRule : IDisposableRuleBase {
        #region Properties

        internal override string Title => "Aderant IDisposable Local Variable Diagnostic";

        internal override string MessageFormat => "Value assigned to variable '{0}' implements 'System.IDisposable' and must be disposed.";

        internal override string Description => "Ensure the object is correctly disposed.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(ProcessNode, SyntaxKind.LocalDeclarationStatement);
        }

        /// <summary>
        /// Processes the local declaration node.
        /// </summary>
        /// <param name="context">The context.</param>
        private void ProcessNode(SyntaxNodeAnalysisContext context) {
            var node = context.Node as LocalDeclarationStatementSyntax;

            // Exit early if execution is not processing a local variable declaration,
            //      or if analysis is suppressed,
            //      or if the variable does not inherit from the System.IDisposable interface.
            if (node == null ||
                IsAnalysisSuppressed(node, ValidSuppressionMessages) ||
                !GetIsNodeDisposable(node, context.SemanticModel)) {
                return;
            }

            // Split a single declaration statement into multiple variable declarators.
            // Example:
            //       object item0, item1;
            var variableDeclaratorExpressions = node.Declaration.Variables;

            // If somehow there are no declarators in the declarations...
            if (!variableDeclaratorExpressions.Any()) {
                // ...exit.
                return;
            }

            // Get the parent method containing the local declaration.
            var parentDeclaration = node
                .Ancestors()
                .FirstOrDefault(syntaxNode => syntaxNode is MemberDeclarationSyntax) as MemberDeclarationSyntax;

            // If somehow there is no parent method...
            if (parentDeclaration == null) {
                // ...exit.
                return;
            }

            var declarationChildNodes = new List<SyntaxNode>(DefaultCapacity * DefaultCapacity);

            // Retrieve every syntax node in the declaration.
            GetExpressionsFromChildNodes(ref declarationChildNodes, parentDeclaration);

            var diagnosticResults = ProcessVariables(
                variableDeclaratorExpressions,
                declarationChildNodes,
                context.SemanticModel);

            // Iterate through all of the 'diagnostic' locations and display them.
            // A single class-level field may result in multiple diagnostics if it is used improperly multiple times.
            foreach (var diagnostic in diagnosticResults) {
                ReportDiagnostic(
                    context,
                    Descriptor,
                    diagnostic.Item3,
                    diagnostic.Item1,
                    diagnostic.Item2);
            }
        }

        /// <summary>
        /// Processes the variables.
        /// </summary>
        /// <param name="declaratorExpressions">The declarator expressions.</param>
        /// <param name="declarationChildNodes">The declaration child nodes.</param>
        /// <param name="semanticModel">The semantic model.</param>
        private static IEnumerable<Tuple<SyntaxNode, string, Location>> ProcessVariables(
            IEnumerable<VariableDeclaratorSyntax> declaratorExpressions,
            IReadOnlyCollection<SyntaxNode> declarationChildNodes,
            SemanticModel semanticModel) {
            var diagnostics = new List<Tuple<SyntaxNode, string, Location>>();

            // Iterate through each declared local variable.
            foreach (var variable in declaratorExpressions) {
                // Determine if the variable is assigned at declaration.
                bool isVariableAssignedAtDeclaration = false;

                // If the declarator's initializer is not null, and not a literal expression.
                if (variable.Initializer != null && !(variable.Initializer.Value is LiteralExpressionSyntax)) {
                    isVariableAssignedAtDeclaration = IsVariableAssigned(variable, semanticModel);
                }

                // Get the variable's name.
                string variableName = variable.Identifier.Text;

                // Retrieve an ordered list of every expression modifying the current variable.
                var orderedExpressions = GetOrderedExpressionTypes(
                    declarationChildNodes,
                    variableName,
                    semanticModel);

                // A null value means a return statement was found to contain the variable.
                if (orderedExpressions == null) {
                    return Enumerable.Empty<Tuple<SyntaxNode, string, Location>>();
                }

                // If the variable is assigned at declaration and
                //       either there are no expressions modifying the variable,
                //       or the first expression is not Disposing the variable.
                if (isVariableAssignedAtDeclaration &&
                    (!orderedExpressions.Any() ||
                     !GetIsNodeFlowControlled(orderedExpressions[0].Item1, variable) &&
                     orderedExpressions[0].Item2 != ExpressionType.Dispose &&
                     orderedExpressions[0].Item2 != ExpressionType.Using &&
                     orderedExpressions[0].Item2 != ExpressionType.CollectionAdd)) {
                    // Add a new diagnostic.
                    diagnostics.Add(
                        new Tuple<SyntaxNode, string, Location>(
                            variable,
                            variableName,
                            variable.Identifier.GetLocation()));
                }

                // Another check to avoid null reference exceptions.
                if (!orderedExpressions.Any()) {
                    continue;
                }

                // Iterate through each of the expressions within the ordered list.
                for (int i = 0; i < orderedExpressions.Count; ++i) {
                    // If the current expression is not an assignment expression...
                    if (orderedExpressions[i].Item2 != ExpressionType.Assignment) {
                        // ...ignore the expression.
                        continue;
                    }

                    // If the current expression is an assignment expression
                    //      and is the final expression in the list...
                    if (i + 1 == orderedExpressions.Count) {
                        diagnostics.Add(new Tuple<SyntaxNode, string, Location>(orderedExpressions[i].Item1, variableName, orderedExpressions[i].Item3));
                        break;
                    }

                    // Ignore any nodes that are 'flow controlled' if/else/switch
                    //      as there could be multiple valid assignments.
                    if (GetIsNodeFlowControlled(orderedExpressions[i].Item1, variable)) {
                        continue;
                    }

                    // If the current expression is an assignment expression
                    //      and the next expression in the list is not some form of disposale or 'Collection Add' expression...
                    if (orderedExpressions[i + 1].Item2 != ExpressionType.Dispose &&
                        orderedExpressions[i + 1].Item2 != ExpressionType.Using &&
                        orderedExpressions[i + 1].Item2 != ExpressionType.CollectionAdd) {
                        // ...add an diagnostic at the expression's location.
                        //      Object is not disposed.
                        diagnostics.Add(new Tuple<SyntaxNode, string, Location>(orderedExpressions[i].Item1, variableName, orderedExpressions[i].Item3));
                    }
                }
            }

            return diagnostics;
        }

        #endregion Methods
    }
}
