using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Analyzer.SQLInjection.Lists;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {
    internal abstract class SqlInjectionRuleBase : RuleBase {
        private const int DefaultCapacity = 25;

        protected enum SqlInjectionRuleViolationSeverity {
            Invalid = -1,
            None,
            Info,
            Warning,
            Error,
            Max
        }

        /// <summary>
        /// Evaluates the node 'command text expression statement'.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="node">The node.</param>
        protected SqlInjectionRuleViolationSeverity EvaluateNodeCommandTextExpressionStatement(
            SemanticModel semanticModel,
            ExpressionStatementSyntax node) {
            // Exit early if it's not actually an 'assignment' expression.
            var assignmentExpression = node.Expression as AssignmentExpressionSyntax;

            if (assignmentExpression == null) {
                return SqlInjectionRuleViolationSeverity.None;
            }

            // Exit early if the assignment expression is not for something called 'CommandText'.
            var memberAccessExpression = assignmentExpression.Left as MemberAccessExpressionSyntax;

            if (memberAccessExpression?.Name.ToString() != "CommandText") {
                return SqlInjectionRuleViolationSeverity.None;
            }

            // Get detailed information regarding the property being accessed.
            var propertySymbol = ModelExtensions.GetSymbolInfo(semanticModel, memberAccessExpression).Symbol as IPropertySymbol;

            if (propertySymbol == null) {
                return SqlInjectionRuleViolationSeverity.None;
            }

            // If the current property is not blacklisted.
            if (!SQLInjectionBlacklists.Properties.Any(value => propertySymbol.ToString().Equals(value.Item2))) {
                return SqlInjectionRuleViolationSeverity.None;
            }

            // Determine if the data being assigned to the property is immutable.
            return IsDataImmutable(semanticModel, assignmentExpression.Right)
                ? SqlInjectionRuleViolationSeverity.None
                : SqlInjectionRuleViolationSeverity.Error;
        }

        /// <summary>
        /// Evaluates the node 'database SQL query'.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="node">The node.</param>
        protected SqlInjectionRuleViolationSeverity EvaluateNodeDatabaseSqlQuery(
            SemanticModel semanticModel,
            InvocationExpressionSyntax node) {
            // Exit early if it's not actually a 'member access' expression, or is not some form of 'SqlQuery'.
            var expression = node.Expression as MemberAccessExpressionSyntax;

            if (expression == null) {
                return SqlInjectionRuleViolationSeverity.None;
            }

            // If the method being invoked is not blacklisted (basic check).
            if (!SQLInjectionBlacklists.Methods.Any(value => expression.Name.ToString().StartsWith(value.Item1))) {
                return SqlInjectionRuleViolationSeverity.None;
            }

            // Get detailed information regarding the method being invoked.
            var methodSymbol = semanticModel.GetSymbolInfo(expression).Symbol as IMethodSymbol;

            // Exit early if no method is found.
            if (methodSymbol == null) {
                return SqlInjectionRuleViolationSeverity.None;
            }

            // If the method being invoked is not blacklisted (detailed check).
            // Or no arguments are passed to the method.
            if (!SQLInjectionBlacklists.Methods.Any(value => methodSymbol.ConstructedFrom.ToString().StartsWith(value.Item2)) ||
                !node.ArgumentList.Arguments.Any()) {
                return SqlInjectionRuleViolationSeverity.None;
            }

            // Determine if the method's first argument contains immutable data.
            return IsDataImmutable(semanticModel, node.ArgumentList.Arguments.First().Expression)
                ? SqlInjectionRuleViolationSeverity.None
                : SqlInjectionRuleViolationSeverity.Error;
        }

        /// <summary>
        /// Evaluates the node 'new SQL command object creation expression'.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="node">The node.</param>
        protected SqlInjectionRuleViolationSeverity EvaluateNodeNewSqlCommandObjectCreationExpression(
            SemanticModel semanticModel,
            ObjectCreationExpressionSyntax node) {
            // Exit early if the node is not creating a new SqlCommand.
            if (!node.ToString().StartsWith("new SqlCommand")) {
                return SqlInjectionRuleViolationSeverity.None;
            }

            // If there were no arguments passed to the creation of the object...
            if (node.ArgumentList == null) {
                // ...determine if the object was created using the object initializer syntax.
                var initializationExpression = node.ChildNodes().OfType<InitializerExpressionSyntax>().FirstOrDefault();

                // If no, objected was created using a parameterless constructor and is not vulnerable to injection at this code point.
                if (initializationExpression == null) {
                    return SqlInjectionRuleViolationSeverity.None;
                }

                // Iterate through all child nodes that are assignment exressions.
                // Expressions can be in any order.
                foreach (var assignmentChildNode in initializationExpression.ChildNodes().OfType<AssignmentExpressionSyntax>()) {
                    if (!string.Equals(assignmentChildNode.Left.ToString(), "CommandText")) {
                        continue;
                    }

                    // Determine if the data being assigned to the 'CommandText' property is immutable.
                    return IsDataImmutable(semanticModel, assignmentChildNode.Right)
                        ? SqlInjectionRuleViolationSeverity.None
                        : SqlInjectionRuleViolationSeverity.Error;
                }
            }

            // If the argument list exists but is empty, there is nothing to inject into.
            if (!node.ArgumentList.Arguments.Any()) {
                return SqlInjectionRuleViolationSeverity.None;
            }

            // Determine if the first argument is immutable.
            return IsDataImmutable(semanticModel, node.ArgumentList.Arguments.First().Expression)
                ? SqlInjectionRuleViolationSeverity.None
                : SqlInjectionRuleViolationSeverity.Error;
        }

        /// <summary>
        /// Evaluates the specified variable, traversing the syntax tree to find data assignments,
        /// recursively iterating through the child objects of the data being assigned
        /// to determine if all data assigned to the variable is immutable.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="variable">The variable.</param>
        /// <param name="methodDeclaration">The method declaration.</param>
        private static bool EvaluateVariable(
            SemanticModel semanticModel,
            SimpleNameSyntax variable,
            MethodDeclarationSyntax methodDeclaration = null) {
            if (IsExpressionNodeConstantString(semanticModel, variable)) {
                return true;
            }

            // Get the parent method of the variable.
            if (methodDeclaration == null) {
                methodDeclaration = GetParentMethodExpression(variable);

                // Scope outside of a method is not supported.
                if (methodDeclaration == null) {
                    return false;
                }
            }

            // Get the latest data assignments for the variable.
            // NOTE:
            //      string foo = "foo";    <--- EqualsValueClauseSyntax
            //      foo = "foo";           <--- AssignmentExpressionSyntax
            //
            //      These two 'assignments' are viewed differently by the Roslyn engine,
            //      and as such must be handled seperately.
            EqualsValueClauseSyntax latestEqualsValueExpression;
            AssignmentExpressionSyntax latestAssignmentExpression;

            // Iterates through all assignment expressions (of both types) returning only the most recent of each.
            GetLatestDataAssignmentExpressions(
                variable,
                methodDeclaration,
                out latestEqualsValueExpression,
                out latestAssignmentExpression);

            // Searches the syntax tree to determine which of the two 'latest' results is actually the most recent,
            // then retrieves the child nodes from that object.
            IEnumerable<SyntaxNode> latestChildren = GetDataAssignmentLatestChildNodes(
                variable,
                methodDeclaration,
                latestEqualsValueExpression,
                latestAssignmentExpression);

            // If no children were retrieved, then no data was assigned to the variable.
            if (latestChildren == null) {
                return false;
            }

            // Iterate through each of the data nodes being assigned to the variable.
            foreach (var childNode in latestChildren) {
                IdentifierNameSyntax childVariable = childNode as IdentifierNameSyntax;

                // If the data being assigned to the target variable, is also a variable...
                if (childVariable != null) {
                    // ...recursively repeat this process for that variable, moving up the syntax tree.
                    if (!EvaluateVariable(semanticModel, childVariable, methodDeclaration)) {
                        return false;
                    }
                } else {
                    // ...otherwise determine if the data is immutable.
                    if (!IsDataImmutable(semanticModel, childNode)) {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Recursively iterates through all child nodes,
        /// adding all nodes to the referenced list.
        /// </summary>
        /// <param name="expressionList">The expression list.</param>
        /// <param name="node">The node.</param>
        /// <param name="stopNode">
        /// Acts as a shortcut in the syntax tree recursive search method.
        /// Searching will stop when this node is found in the tree.
        /// </param>
        private static void GetAllExpressionsFromChildNodes(
            ref List<SyntaxNode> expressionList,
            SyntaxNode node,
            SyntaxNode stopNode = null) {
            foreach (var childNode in node.ChildNodes()) {
                if (childNode.Equals(stopNode)) {
                    return;
                }

                expressionList.Add(childNode);

                if (childNode.ChildNodes().Any()) {
                    GetAllExpressionsFromChildNodes(ref expressionList, childNode, stopNode);
                }
            }
        }

        /// <summary>
        /// Recursively iterates through all child nodes,
        /// adding any expressions of the specified type to the referenced list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expressionList">The invocation expression list.</param>
        /// <param name="node">The node.</param>
        /// <param name="stopNode">
        /// Acts as a shortcut in the syntax tree recursive search method.
        /// Searching will stop when this node is found in the tree.
        /// </param>
        /// <returns>True if the 'StopNode' has been found, otherwise False.</returns>
        private static bool GetExpressionsFromChildNodes<T>(
            ref List<T> expressionList,
            SyntaxNode node,
            SyntaxNode stopNode = null) where T : SyntaxNode {
            foreach (var childNode in node.ChildNodes()) {
                // Syntax tree navigation will cease if this node is found.
                if (childNode.Equals(stopNode)) {
                    return true;
                }

                var expression = childNode as T;

                if (expression == null) {
                    // Recursion.
                    if (GetExpressionsFromChildNodes(ref expressionList, childNode, stopNode)) {
                        return true;
                    }
                } else {
                    expressionList.Add(expression);
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the method expression for the parent method of the specified node.
        /// </summary>
        /// <param name="node">The node.</param>
        private static MethodDeclarationSyntax GetParentMethodExpression(SyntaxNode node) {
            return node.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        }

        /// <summary>
        /// Gets the latest data assignment expressions for the specified variable from the specified starting node.
        /// </summary>
        /// <param name="variable">The variable.</param>
        /// <param name="node">The node.</param>
        /// <param name="latestEqualsValueExpression">The latest equals value expression.</param>
        /// <param name="latestAssignmentExpression">The latest assignment expression.</param>
        private static void GetLatestDataAssignmentExpressions(
            SimpleNameSyntax variable,
            SyntaxNode node,
            out EqualsValueClauseSyntax latestEqualsValueExpression,
            out AssignmentExpressionSyntax latestAssignmentExpression) {
            List<EqualsValueClauseSyntax> equalsValueExpressions = new List<EqualsValueClauseSyntax>(DefaultCapacity);
            List<AssignmentExpressionSyntax> assignmentExpressions = new List<AssignmentExpressionSyntax>(DefaultCapacity);

            // Recursively search for nodes of the specified type, stopping if this variable is found during the search.
            // Data assignments after this node are irrelevant to the current context.
            GetExpressionsFromChildNodes(ref equalsValueExpressions, node, variable);
            GetExpressionsFromChildNodes(ref assignmentExpressions, node, variable);

            // If results were returned, grab the latest.
            latestEqualsValueExpression = equalsValueExpressions.Any()
                ? equalsValueExpressions.LastOrDefault(x => x.Parent.ToString().StartsWith(variable.Identifier.ToString()))
                : null;

            // If results were returned, grab the latest.
            latestAssignmentExpression = assignmentExpressions.Any()
                ? assignmentExpressions.LastOrDefault(x => x.Left.ToString().StartsWith(variable.Identifier.ToString()))
                : null;
        }

        /// <summary>
        /// Gets the child nodes of the latest data assignment expression.
        /// </summary>
        /// <param name="variable">The variable.</param>
        /// <param name="node">The node.</param>
        /// <param name="latestEqualsValueExpression">The latest equals value expression.</param>
        /// <param name="latestAssignmentExpression">The latest assignment expression.</param>
        private static IEnumerable<SyntaxNode> GetDataAssignmentLatestChildNodes(
            SyntaxNode variable,
            SyntaxNode node,
            EqualsValueClauseSyntax latestEqualsValueExpression,
            AssignmentExpressionSyntax latestAssignmentExpression) {
            // If both expression types were found, determine which of the two is actually the 'latest'.
            if (latestEqualsValueExpression != null && latestAssignmentExpression != null) {
                List<SyntaxNode> allExpressions = new List<SyntaxNode>(DefaultCapacity * 5);

                // Gets *all* child expressions, in 'top to bottom' order, stopping at the target node.
                GetAllExpressionsFromChildNodes(ref allExpressions, node, variable);

                // Reverses the results, such that the most recent expressions appear first.
                allExpressions.Reverse();

                // Iterate through the result expressions, finding either of the two assignment expressions.
                // Return the first result found.
                foreach (var expression in allExpressions) {
                    if (expression.Equals(latestAssignmentExpression)) {
                        return latestAssignmentExpression.ChildNodes().Where(x => !x.Equals(latestAssignmentExpression.Left));
                    }

                    if (!expression.Equals(latestEqualsValueExpression)) {
                        continue;
                    }

                    return latestEqualsValueExpression.ChildNodes();
                }
            } else if (latestEqualsValueExpression != null) {
                return latestEqualsValueExpression.ChildNodes();
            } else if (latestAssignmentExpression != null) {
                return latestAssignmentExpression.ChildNodes().Where(x => !x.Equals(latestAssignmentExpression.Left));
            }

            return null;
        }

        /// <summary>
        /// Examines the specified context to determine if the method containing
        /// the targeted node includes a code analysis suppression attribute.
        /// </summary>
        /// <param name="context">The context.</param>
        protected static bool IsAnalysisSuppressed(SyntaxNodeAnalysisContext context) {
            // Get the parent method of the node.
            MethodDeclarationSyntax methodDeclaration = GetParentMethodExpression(context.Node);

            // Scope outside of a method is not supported.
            if (methodDeclaration == null) {
                return false;
            }

            // Attributes can be stacked...
            // [Attribute()]
            // [Attribute()]
            // ...and concatenated...
            // [Attribute(), Attribute()]
            // The below double loop handles both cases, including mix & match.
            foreach (AttributeListSyntax attributeList in methodDeclaration.ChildNodes().OfType<AttributeListSyntax>()) {
                foreach (AttributeSyntax attribute in attributeList.Attributes) {
                    string name = attribute.Name.ToString();

                    if (!name.Equals("SuppressMessage") && !name.Equals("System.Diagnostics.CodeAnalysis.SuppressMessage")) {
                        continue;
                    }

                    string category = attribute.ArgumentList.Arguments[0].ToString();
                    string checkId = attribute.ArgumentList.Arguments[1].ToString();

                    if (category.Equals("\"SQL Injection\"", StringComparison.OrdinalIgnoreCase) &&
                        checkId.StartsWith("\"Aderant_SqlInjection", StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }

                    if (category.Equals("\"Microsoft.Security\"", StringComparison.OrdinalIgnoreCase) &&
                        checkId.StartsWith("\"CA2100:", StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the specified node is immutable data.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="node">The node.</param>
        /// <param name="methodDeclaration">The method declaration.</param>
        private static bool IsDataImmutable(
            SemanticModel semanticModel,
            SyntaxNode node,
            MethodDeclarationSyntax methodDeclaration = null) {
            // Get the parent method for this node.
            if (methodDeclaration == null) {
                methodDeclaration = GetParentMethodExpression(node);

                // If the node is not within a method, use case is out of scope.
                if (methodDeclaration == null) {
                    return false;
                }
            }

            // If the current node has children, evaluate the children, otherwise evaluate the current node.
            // Example:
            //      string foo = someCondition ? someValue : someOtherValue;
            //      The 'node' is the conditional expression, so the expression needs to be pulled apart into its child nodes:
            //      Condition: someCondition
            //      WhenTrue:  someValue
            //      WhenFalse: someOtherValue
            IEnumerable<SyntaxNode> nodesToEvaluate = node.ChildNodes().Any()
                ? node.ChildNodes()
                : new[] { node };

            foreach (var childNode in nodesToEvaluate) {
                // Determine if the node is a constant string, string literal, or is whitelisted.
                if (IsExpressionNodeValid(semanticModel, childNode)) {
                    continue;
                }

                // If the childNode is a binary expression...
                // Example:                                      v------- Binary  Expression -------v
                //      string foo = someCondition ? someValue : someOtherValue + someOtherOtherValue;
                BinaryExpressionSyntax addExpression = childNode as BinaryExpressionSyntax;

                if (addExpression != null) {
                    // ...ensure it's an 'add' expression.
                    if (addExpression.Kind() != SyntaxKind.AddExpression) {
                        return false;
                    }

                    // Recursively determine if both sides of the expression contain immutable data.
                    if (!IsDataImmutable(semanticModel, addExpression.Left, methodDeclaration) ||
                        !IsDataImmutable(semanticModel, addExpression.Right, methodDeclaration)) {
                        return false;
                    }

                    continue;
                }

                // If the parent node is a conditional expression...
                ConditionalExpressionSyntax conditionalExpression = node as ConditionalExpressionSyntax;

                // ...and the parent node's 'Condition' node is the current child node...
                if (conditionalExpression != null && conditionalExpression.Condition == childNode) {
                    continue;
                }

                // If the child node is a variable...
                IdentifierNameSyntax identifierName = childNode as IdentifierNameSyntax;

                if (identifierName != null) {
                    // Traverse the syntax tree to determine if the data assigned to the variable is immutable.
                    if (EvaluateVariable(semanticModel, identifierName, methodDeclaration)) {
                        continue;
                    }

                    return false;
                }

                // If the child node is also a conditional expression...
                conditionalExpression = childNode as ConditionalExpressionSyntax;

                // Evaluate both the WhenTrue and WhenFalse expressions.
                if (conditionalExpression == null ||
                    !IsDataImmutable(semanticModel, conditionalExpression.WhenTrue, methodDeclaration) ||
                    !IsDataImmutable(semanticModel, conditionalExpression.WhenFalse, methodDeclaration)) {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified expression is a constant string from the System namespace, or a string literal.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="expression">The expression.</param>
        private static bool IsExpressionNodeConstantString(SemanticModel semanticModel, SyntaxNode expression) {
            if (expression is LiteralExpressionSyntax) {
                return true;
            }

            // Get detailed information regarding the expression.
            ISymbol symbol = semanticModel.GetSymbolInfo(expression).Symbol;

            var localSymbol = symbol as ILocalSymbol;

            // If the expression is a local variable,
            // Return a severity of 'None' if the variable is a constant string from the 'System' namespace.
            // Otherwise return a severity of 'Error'.
            if (localSymbol != null) {
                return localSymbol.HasConstantValue &&
                       localSymbol.Type.ToString().Equals("string") &&
                       localSymbol.Type.ContainingNamespace.ToString().Equals("System");
            }

            var fieldSymbol = symbol as IFieldSymbol;

            // If the expression is a class-level field,
            // Return a severity of 'None' if the field is a constant string from the 'System' namespace.
            // Otherwise return a severity of 'Error'.
            if (fieldSymbol == null) {
                return false;
            }

            return fieldSymbol.IsConst &&
                   fieldSymbol.Type.ToString().Equals("string") &&
                   fieldSymbol.Type.ContainingNamespace.ToString().Equals("System");
        }

        /// <summary>
        /// Determines whether the specified expression is valid.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="expression">The expression.</param>
        private static bool IsExpressionNodeValid(SemanticModel semanticModel, SyntaxNode expression) {
            return IsExpressionNodeConstantString(semanticModel, expression) ||
                   IsExpressionNodeWhitelisted(semanticModel, expression);
        }

        /// <summary>
        /// Determines whether the specified expression is whitelisted.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="expression">The expression.</param>
        private static bool IsExpressionNodeWhitelisted(SemanticModel semanticModel, SyntaxNode expression) {
            // Determines whether the specified node's parent is a member access expression,
            // operating on the Properties.Resources member.
            var memberAccessExpression = expression.Parent as MemberAccessExpressionSyntax;

            if (memberAccessExpression != null) {
                if (IsExpressionNodeResources(memberAccessExpression)) {
                    return true;
                }
            }

            // Determines whether the current node is a valid member access expression.
            memberAccessExpression = expression as MemberAccessExpressionSyntax;

            if (memberAccessExpression == null) {
                return false;
            }

            // Attempts to retrieve additional type data relating to the specified node.
            var typeSymbol = semanticModel.GetSymbolInfo(memberAccessExpression).Symbol as INamedTypeSymbol;

            // If no additional data was found, determine if the current node is referenceing the Properties.Resources member.
            // Otherwise evaluate the specified node against the whitelists.
            return typeSymbol == null
                ? IsExpressionNodeResources(memberAccessExpression)
                : SQLInjectionWhitelists.Properties.Any(value => typeSymbol.OriginalDefinition.ToString().Contains(value.Item2));
        }

        /// <summary>
        /// Determines whether the specified member access expression is for the Properties.Resources member.
        /// </summary>
        /// <param name="memberAccessExpression">The member access expression.</param>
        private static bool IsExpressionNodeResources(MemberAccessExpressionSyntax memberAccessExpression) {
            if (memberAccessExpression == null) {
                return false;
            }

            string expressionString = memberAccessExpression.ToString();

            // If the expression begins with 'Resources.',
            // evaluate the using directives at the top of the file to ensure there's a reference to '.Properties'.
            return !expressionString.StartsWith("Resources.")
                ? expressionString.Contains("Properties.Resources.")
                : memberAccessExpression.Ancestors().OfType<CompilationUnitSyntax>().First()
                    .ChildNodes().OfType<UsingDirectiveSyntax>()
                    .Any(usingDirective => usingDirective.Name.ToString().EndsWith(".Properties"));
        }
    }
}
