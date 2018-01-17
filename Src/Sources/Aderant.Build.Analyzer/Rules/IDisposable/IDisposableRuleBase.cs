using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Analyzer.Extensions;
using Aderant.Build.Analyzer.Lists.IDisposable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aderant.Build.Analyzer.Rules.IDisposable {
    internal abstract class IDisposableRuleBase : RuleBase {
        #region Types

        /// <summary>
        /// The type of the declared collection.
        /// </summary>
        protected enum DeclarationCollectionType {
            Collection,
            Dictionary,
            List,
            None
        }

        /// <summary>
        /// The type of expression for use in ordering expressions.
        /// </summary>
        protected enum ExpressionType {
            None,
            Assignment,
            AssignmentNull,
            Dispose,
            Using,
            UsingAssignment,
            Exit
        }

        /// <summary>
        /// Data container for disposable property and field declarations.
        /// </summary>
        protected struct DisposableDeclaration {
            public DisposableDeclaration(
                SyntaxNode node,
                string name,
                Location location,
                bool isAssignedAtDeclaration,
                bool isStatic,
                DeclarationCollectionType collection) {
                Node = node;
                Name = name;
                IsAssignedAtDeclaration = isAssignedAtDeclaration;
                IsStatic = isStatic;
                Location = location;
                CollectionType = collection;
            }

            public DeclarationCollectionType CollectionType { get; }

            public Location Location { get; }

            public bool IsAssignedAtDeclaration { get; }

            public bool IsStatic { get; }

            public string Name { get; }

            public SyntaxNode Node { get; }
        }

        #endregion Types

        #region Fields

        protected const string DiagnosticId = "Aderant_IDisposableDiagnostic";

        internal static Tuple<string, string>[] ValidSuppressionMessages = {
            new Tuple<string, string>("\"IDisposable\"", "\"Aderant_IDisposableDiagnostic\"")
        };

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

        internal override string Id => DiagnosticId;

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Error;

        #endregion Properties

        #region Methods: Protected

        /// <summary>
        /// Returns true if the specified type symbol implements the System.IDisposable interface.
        /// Return true if:
        ///      The symbol was found.
        ///      The symbol is named 'IDisposable'.
        ///      The symbol is from the namespace 'System'.
        /// </summary>
        /// <param name="symbol">The symbol.</param>
        protected static bool GetIsDisposable(ITypeSymbol symbol) {
            if (symbol == null) {
                return false;
            }

            if (string.Equals("System.IDisposable", symbol.OriginalDefinition.ToDisplayString(), StringComparison.Ordinal)) {
                return true;
            }

            return !IDisposableWhitelist.Types.Any(x => x.Item1 == symbol.ContainingNamespace.ToString() && x.Item2 == symbol.Name) &&
                   symbol.AllInterfaces.Any(
                       namedTypeSymbol =>
                           namedTypeSymbol.Name.Equals("IDisposable", StringComparison.Ordinal) &&
                           namedTypeSymbol.ContainingNamespace.Name.Equals("System", StringComparison.Ordinal));
        }

        /// <summary>
        /// Determines whether the specified node is whitelisted.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="semanticModel">The semantic model.</param>
        protected static bool GetIsClassNodeWhitelisted(BaseTypeDeclarationSyntax node, SemanticModel semanticModel) {
            string classTypeString = semanticModel.GetDeclaredSymbol(node)?.OriginalDefinition?.ToDisplayString();

            return !string.IsNullOrWhiteSpace(classTypeString) &&
                   IDisposableWhitelist
                       .Types
                       .Select(tuple => string.Concat(tuple.Item1, ".", tuple.Item2))
                       .Any(typeString => typeString.Equals(classTypeString, StringComparison.Ordinal));
        }

        /// <summary>
        /// Determines if the specified declaration modifiers contain the 'static' keyword.
        /// </summary>
        /// <param name="modifiers">The modifiers.</param>
        protected static bool GetIsDeclarationStatic(SyntaxTokenList modifiers) {
            return modifiers.Any(modifier => modifier.Text.Equals("static", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines if the specified node inherits from the 'System.IDisposable' interface.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="semanticModel">The semantic model.</param>
        protected static bool GetIsNodeDisposable(SyntaxNode node, SemanticModel semanticModel) {
            ITypeSymbol symbol;

            if (node is InvocationExpressionSyntax) {
                // If the node is an invocation expression,
                //      get the symbol for the invocation expression's return type.
                symbol = (semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol)?.ReturnType;
            }  else if (node is FieldDeclarationSyntax) {
                // If the node is a field declaration expression,
                //      get the relevant symbol from the field declaration, includes handling for generic collections.
                symbol = GetFieldDeclarationSymbol((FieldDeclarationSyntax)node, semanticModel);
            } else if (node is LocalDeclarationStatementSyntax) {
                // If the node is a local declaration statement syntax,
                //      get the symbol for the declaration's type symbol.
                symbol = semanticModel.GetTypeInfo(((LocalDeclarationStatementSyntax)node).Declaration.Type).Type;
            } else if (node is ClassDeclarationSyntax) {
                // If the node is a class declaration syntax,
                //      get the class' type symbol.
                symbol = semanticModel.GetDeclaredSymbol(node) as ITypeSymbol;
            } else if (node is PropertyDeclarationSyntax) {
                // If the node is a property declaration syntax,
                //      get the property's type's type symbol.
                symbol = semanticModel.GetTypeInfo(((PropertyDeclarationSyntax)node).Type).Type;
            } else if (node is ObjectCreationExpressionSyntax) {
                // If the node is an object creation syntax,
                //      get the object's type symbol.
                symbol = semanticModel.GetSymbolInfo(((ObjectCreationExpressionSyntax)node).Type).Symbol as ITypeSymbol;
            } else if (node is TypeSyntax) {
                // If the node is a type syntax
                //      get the type's type symbol
                symbol = semanticModel.GetTypeInfo((TypeSyntax)node).Type;
            } else if (node is ParameterSyntax) {
                // If the node is a parameter syntax
                //      get the declared type's symbol
                symbol = (semanticModel.GetDeclaredSymbol(node) as IParameterSymbol)?.Type;
            } else {
                // If the node type is unhandled, exit.
                return false;
            }

            return GetIsDisposable(symbol);
        }

        /// <summary>
        /// Gets the field declaration symbol.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="semanticModel">The semantic model.</param>
        protected static ITypeSymbol GetFieldDeclarationSymbol(
            FieldDeclarationSyntax node,
            SemanticModel semanticModel) {
            SyntaxNode typeNode = node.Declaration?.ChildNodes()?.FirstOrDefault();

            // If somehow the child node could not be found...
            if (typeNode == null) {
                // ...exit.
                return null;
            }

            // Get the symbol for the previously located type node.
            var typeSymbol = semanticModel.GetTypeInfo(typeNode).Type;

            var displayString = typeSymbol?
                .OriginalDefinition
                .ToDisplayString();

            if (string.IsNullOrWhiteSpace(displayString)) {
                return typeSymbol;
            }

            if (string.Equals("System.Collections.Generic.Dictionary<TKey, TValue>", displayString, StringComparison.Ordinal)) {
                return (semanticModel.GetTypeInfo(node.Declaration.Type).Type as INamedTypeSymbol)?.TypeArguments[1];
            }

            if (displayString.StartsWith("System.Collections.Generic.", StringComparison.Ordinal) ||
                displayString.StartsWith("System.Collections.ObjectModel.", StringComparison.Ordinal)) {
                return (semanticModel.GetTypeInfo(node.Declaration.Type).Type as INamedTypeSymbol)?.TypeArguments[0];
            }

            return typeSymbol;
        }

        /// <summary>
        /// Retrieves a list of expression types that modify or operate on
        /// the specified field, from among the specified expressions.
        /// The resulting list is ordered to mirror the layout of the code file.
        /// </summary>
        /// <param name="expressions">The expressions.</param>
        /// <param name="variableName">Name of the variable.</param>
        protected static List<Tuple<SyntaxNode, ExpressionType, Location>> GetOrderedExpressionTypes(
            IEnumerable<SyntaxNode> expressions,
            string variableName) {
            var orderedExpressionTypes = new List<Tuple<SyntaxNode, ExpressionType, Location>>(DefaultCapacity);

            // Iterate through each of the provided expressions.
            // If a use case is discovered that guarantees there are no further expressions,
            //     a null value is returned to shortcut execution.
            foreach (var expression in expressions) {
                // Attempt to evaluate the expression as a return statement.
                // If the result is to exit...
                if (EvaluateReturnStatement(
                        expression as ReturnStatementSyntax,
                        variableName) == ExpressionType.Exit) {
                    // ...exit.
                    return null;
                }

                // Add ordered assignment expressions, returning null if there are guaranteed to be no valid expressions.
                if (AddOrderedAssignmentExpressions(ref orderedExpressionTypes, expression, variableName)) {
                    return null;
                }

                // Attempt to evaluate the expression as an invocation expression.
                // If the result confirms that the expression is a 'Dispose' invocation expression...
                if (EvaluateInvocationExpression(
                        expression as InvocationExpressionSyntax,
                        variableName) == ExpressionType.Dispose) {
                    // ...add a new 'Dispose' expression to the list of ordered expressions.
                    orderedExpressionTypes.Add(
                        new Tuple<SyntaxNode, ExpressionType, Location>(
                            expression,
                            ExpressionType.Dispose,
                            expression.GetLocation()));

                    continue;
                }

                // Add ordered conditional and using statement expressions.
                AddOrderedConditionalExpressions(ref orderedExpressionTypes, expression, variableName);
                AddOrderedUsingExpressions(ref orderedExpressionTypes, expression, variableName);
            }

            // After iterating through every expression in the method,
            //     return the final ordered list of expressions.
            return orderedExpressionTypes;
        }

        #endregion Methods: Protected

        #region Methods: Private

        /// <summary>
        /// Adds the ordered assignment expressions.
        /// </summary>
        /// <param name="orderedExpressionTypes">The ordered expression types.</param>
        /// <param name="node">The node.</param>
        /// <param name="variableName">Name of the variable.</param>
        /// <returns>if set to <c>true</c> [exit evaluation early] otherwise [continue processing]</returns>
        private static bool AddOrderedAssignmentExpressions(
            ref List<Tuple<SyntaxNode, ExpressionType, Location>> orderedExpressionTypes,
            SyntaxNode node,
            string variableName) {
            // Attempt to evaluate the expression as an assignment expression.
            var assignmentExpression = node as AssignmentExpressionSyntax;

            // If the node is not an assignment node,
            //      or the node's parent is an initializer expression...
            if (assignmentExpression == null || assignmentExpression.Parent is InitializerExpressionSyntax) {
                // ...skip processing for assignment expressions.
                return false;
            }

            if (!IsVariableAssigned(node)) {
                return false;
            }

            var assignmentExpressionType = EvaluateAssignmentExpression(assignmentExpression, variableName);

            switch (assignmentExpressionType) {
                // If the result is to exit...
                case ExpressionType.Exit: {
                    // ...exit early.
                    return true;
                }
                // If the result is an assignment expression (including a null assignment)...
                case ExpressionType.Assignment:
                case ExpressionType.AssignmentNull: {
                    // ...add a new expression of the returned type, to the list of ordered expressions.
                    orderedExpressionTypes.Add(
                        new Tuple<SyntaxNode, ExpressionType, Location>(
                            assignmentExpression.Left,
                            assignmentExpressionType,
                            assignmentExpression.Left.GetLocation()));
                    break;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the ordered conditional expressions.
        /// </summary>
        /// <param name="orderedExpressionTypes">The ordered expression types.</param>
        /// <param name="node">The node.</param>
        /// <param name="variableName">Name of the variable.</param>
        private static void AddOrderedConditionalExpressions(
            ref List<Tuple<SyntaxNode, ExpressionType, Location>> orderedExpressionTypes,
            SyntaxNode node,
            string variableName) {
            var conditionalExpression = node as ConditionalAccessExpressionSyntax;

            string name = (conditionalExpression?.Expression as IdentifierNameSyntax)?.Identifier.Text;
            string expression = (conditionalExpression?.WhenNotNull as InvocationExpressionSyntax)?.Expression.ToString();

            // Attempt to evaluate the expression as a conditional expression.
            //      If the 'Expression' is the variable and the 'WhenNotNull' is a Dispose()/Close() method invocation...
            if (name?.Equals(variableName, StringComparison.Ordinal) == true &&
                (expression?.Equals(".Dispose", StringComparison.Ordinal) == true ||
                 expression?.Equals(".Close", StringComparison.Ordinal) == true)) {
                // ...add a new 'Dispose' expression to the list of ordered expressions.
                orderedExpressionTypes.Add(
                    new Tuple<SyntaxNode, ExpressionType, Location>(
                        node,
                        ExpressionType.Dispose,
                        node.GetLocation()));
            }
        }

        /// <summary>
        /// Adds the ordered using expressions.
        /// </summary>
        /// <param name="orderedExpressionTypes">The ordered expression types.</param>
        /// <param name="node">The node.</param>
        /// <param name="variableName">Name of the variable.</param>
        private static void AddOrderedUsingExpressions(
            ref List<Tuple<SyntaxNode, ExpressionType, Location>> orderedExpressionTypes,
            SyntaxNode node,
            string variableName) {
            // Attempt to evaluate the expression as a using statement expression.
            var usingExpression = node as UsingStatementSyntax;

            if (usingExpression == null) {
                return;
            }

            string itemName;
            ExpressionType expressionType;

            var assignmentExpression = usingExpression.ChildNodes().ElementAt(0) as AssignmentExpressionSyntax;

            if (assignmentExpression != null) {
                itemName = (assignmentExpression.Left as IdentifierNameSyntax)?.Identifier.Text;
                expressionType = ExpressionType.UsingAssignment;
            } else {
                itemName = (usingExpression.ChildNodes()?.ElementAt(0) as IdentifierNameSyntax)?.Identifier.Text;
                expressionType = ExpressionType.Using;
            }

            // If the variable being 'used' has the same name as the variable being operated upon...
            if (itemName?.Equals(variableName) == true) {
                // ...add a new 'UsingAssignment' expression to the list of ordered expressions.
                orderedExpressionTypes.Add(new Tuple<SyntaxNode, ExpressionType, Location>(node, expressionType, node.GetLocation()));
            }
        }

        /// <summary>
        /// Evaluates the assignment expression for the specified field.
        /// </summary>
        /// <param name="assignmentExpression">The assignment expression.</param>
        /// <param name="fieldName">Name of the field.</param>
        /// <returns>
        /// The type of assignment expression.
        /// </returns>
        private static ExpressionType EvaluateAssignmentExpression(
            AssignmentExpressionSyntax assignmentExpression,
            string fieldName) {

            // If execution is not processing an assignment expression,
            //     or the assignment is occuring within a using statement...
            if (assignmentExpression == null ||
                assignmentExpression.Parent is UsingStatementSyntax) {
                // ...return no expression.
                return ExpressionType.None;
            }

            // Retrieve the children of the assignment expression.
            // It is expected that there will be two primary children:
            //     1. The variable being assigned to.
            //     2. The value being assigned.
            var assignmentChildNodes = assignmentExpression.ChildNodes().ToList();

            // Examine the first child, this should be an identifier
            //     containing the name of the variable being assigned to.
            var identifier = assignmentChildNodes[0] as IdentifierNameSyntax;

            // If the child was not an identifier, or was not the field being operated on...
            if (identifier == null ||
                !identifier.Identifier.ToString().Equals(fieldName, StringComparison.Ordinal)) {
                return (assignmentChildNodes[1] as IdentifierNameSyntax)?
                       .Identifier.ToString().Equals(fieldName, StringComparison.Ordinal) == true
                    ? ExpressionType.Exit
                    : ExpressionType.None;
            }

            // The second child should be the assignment value.
            // If the assignment value is a literal syntax,
            //    the only literal that can be assigned to an IDisposable object, is null.
            return assignmentChildNodes[1] is LiteralExpressionSyntax
                ? ExpressionType.AssignmentNull
                : ExpressionType.Assignment;
        }

        /// <summary>
        /// Evaluates the conditional expression.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="variableName">Name of the variable.</param>
        private static ExpressionType EvaluateConditionalExpression(ConditionalExpressionSyntax node, string variableName) {
            // If node is not a conditional expression...
            if (node == null) {
                // ...shortcut execution.
                return ExpressionType.None;
            }

            // Recursively evaluate the 'WhenTrue' clause of the conditional expression, returning an exit expression if found.
            var paren = node.WhenTrue as ParenthesizedExpressionSyntax;
            var conditional = paren != null
                ? paren.Expression as ConditionalExpressionSyntax
                : node.WhenTrue as ConditionalExpressionSyntax;

            if (EvaluateConditionalExpression(conditional, variableName) == ExpressionType.Exit) {
                return ExpressionType.Exit;
            }

            // Recursively evaluate the 'WhenFalse' clause of the conditional expression, returning an exit expression if found.
            paren = node.WhenFalse as ParenthesizedExpressionSyntax;
            conditional = paren != null
                ? paren.Expression as ConditionalExpressionSyntax
                : node.WhenFalse as ConditionalExpressionSyntax;

            if (EvaluateConditionalExpression(conditional, variableName) == ExpressionType.Exit) {
                return ExpressionType.Exit;
            }

            // Evaluate the 'WhenTrue' for an identifier matching the specified variable name.
            var identifier = node.WhenTrue as IdentifierNameSyntax;

            if (identifier != null &&
                identifier.Identifier.Text.Equals(variableName, StringComparison.Ordinal)) {
                return ExpressionType.Exit;
            }

            // Evaluate the 'WhenFalse' for an identifier matching the specified variable name.
            identifier = node.WhenFalse as IdentifierNameSyntax;

            return identifier != null &&
                   identifier.Identifier.Text.Equals(variableName, StringComparison.Ordinal)
                ? ExpressionType.Exit
                : ExpressionType.None;
        }

        /// <summary>
        /// Evaluates the invocations expression for the specified field.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="variableName">Name of the variable.</param>
        /// <returns>
        /// The type of invocation expression.
        /// </returns>
        private static ExpressionType EvaluateInvocationExpression(
            InvocationExpressionSyntax node,
            string variableName) {
            var accessExpression = node?.Expression as MemberAccessExpressionSyntax;

            // Get all the 'Identifiers' from among the invocation expression's children.
            // It is expected that there are two results:
            //     1. The name of the object being operated upon.
            //     2. The name of the method being invoked.
            var identifierNames = accessExpression?
                .ChildNodes()
                .OfType<IdentifierNameSyntax>()
                .Select(x => x.Identifier.ToString())
                .ToList();

            // If the first identifier is the name of the field being processed,
            //     and the second is the Dispose method (or the Close method)...
            if (identifierNames != null &&
                identifierNames.Count > 1 &&
                identifierNames[0].Equals(variableName, StringComparison.Ordinal) &&
                (identifierNames[1].Equals("Dispose", StringComparison.Ordinal) ||
                 identifierNames[1].Equals("Close", StringComparison.Ordinal))) {
                // ...return the dispose expression type.
                return ExpressionType.Dispose;
            }

            // Otherwise, return no expression type.
            return ExpressionType.None;
        }

        /// <summary>
        /// Evaluates the return statement.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="variableName">Name of the variable.</param>
        private static ExpressionType EvaluateReturnStatement(SyntaxNode node, string variableName) {
            var statement = node as ReturnStatementSyntax;

            // Exit early if the node is not a return statement.
            if (statement == null) {
                return ExpressionType.None;
            }

            // Determine if the return statement is a 'simple' return statement.
            // Example:
            //     return item;
            var result = statement
                .ChildNodes()
                .OfType<IdentifierNameSyntax>()
                .Any(identifier => identifier.Identifier.Text.Equals(variableName, StringComparison.Ordinal))
                ? ExpressionType.Exit
                : ExpressionType.None;

            // If the return statement is simply returning the variable,
            //     ignore the variable and exit.
            if (result == ExpressionType.Exit) {
                return result;
            }

            // The return expression contains one-to-many conditional expressions that must be evaluated.
            var condition = statement.ChildNodes().OfType<ConditionalExpressionSyntax>().FirstOrDefault();

            return EvaluateConditionalExpression(condition, variableName);
        }

        /// <summary>
        /// Determines whether the specified node contains child nodes of a type indicating variable assignment.
        /// </summary>
        /// <param name="variable">The variable.</param>
        protected static bool IsVariableAssigned(SyntaxNode variable) {
            // Get all of the child nodes for the declarator.
            var declaratorSyntaxNodes = new List<SyntaxNode>();
            GetExpressionsFromChildNodes(ref declaratorSyntaxNodes, variable);

            // Iterate through each child node.
            foreach (var syntaxNode in declaratorSyntaxNodes) {
                // If the child node is neither an object creation nor an invocation expression...
                if (!(syntaxNode is ObjectCreationExpressionSyntax || syntaxNode is InvocationExpressionSyntax)) {
                    // ...ignore it.
                    continue;
                }

                // Otherwise, attempt to get the current child node's parent argument list.
                // If an argument list is found, then the current child node is a parameter to a method, and can be ignored.
                if (syntaxNode.GetAncestorOfType<ArgumentListSyntax>() != null) {
                    continue;
                }

                // Otherwise, the variable is assigned and must be disposed.
                return true;
            }

            // Variable is not assigned.
            return false;
        }

        #endregion Methods: Private
    }
}
