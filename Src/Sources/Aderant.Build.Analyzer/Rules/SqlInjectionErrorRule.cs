using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Analyzer.Extensions;
using Aderant.Build.Analyzer.Lists.SQLInjection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {
    internal class SqlInjectionErrorRule : RuleBase {
        internal const string DiagnosticId = "Aderant_SqlInjectionError";

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Error;

        internal override string Id => DiagnosticId;

        internal override string Title => "SQL Injection Error";

        internal override string MessageFormat => "Database command is vulnerable to SQL injection. " +
                                                  "Consider parameterizing the command or using the Stored Procedure DSL.";

        internal override string Description => "Use Stored Procedure DSL.";

        public override DiagnosticDescriptor Descriptor => new DiagnosticDescriptor(
            Id,
            Title,
            MessageFormat,
            AnalyzerCategory.Syntax,
            Severity,
            true,
            Description,
            "http://ttwiki/wiki/index.php?title=Stored_Procedure_DSL");

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNodeCommandText, SyntaxKind.ExpressionStatement);
            context.RegisterSyntaxNodeAction(AnalyzeNodeDatabaseSqlQuery, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeNodeNewSqlCommand, SyntaxKind.ObjectCreationExpression);
        }

        /// <summary>
        /// Analyzes the 'command text' node.
        /// </summary>
        /// <param name="context">The context.</param>
        private void AnalyzeNodeCommandText(SyntaxNodeAnalysisContext context) {
            var expression = context.Node as ExpressionStatementSyntax;

            if (expression == null ||
                IsAnalysisSuppressed(expression, DiagnosticId) ||
                IsAnalysisSuppressed(expression, "CA2100")) {
                return;
            }

            Location location = null;

            if (EvaluateNodeCommandTextExpressionStatement(ref location, context.SemanticModel, expression) != RuleViolationSeverityEnum.Error) {
                return;
            }

            if (location == null) {
                location = context.Node.GetLocation();
            }

            ReportDiagnostic(context, Descriptor, location, expression);
        }

        /// <summary>
        /// Analyzes the 'database SQL query' node.
        /// </summary>
        /// <param name="context">The context.</param>
        private void AnalyzeNodeDatabaseSqlQuery(SyntaxNodeAnalysisContext context) {
            var expression = context.Node as InvocationExpressionSyntax;

            if (expression == null ||
                IsAnalysisSuppressed(expression, DiagnosticId) ||
                IsAnalysisSuppressed(expression, "CA2100")) {
                return;
            }

            Location location = null;

            if (EvaluateNodeDatabaseSqlQuery(ref location, context.SemanticModel, expression) != RuleViolationSeverityEnum.Error) {
                return;
            }

            if (location == null) {
                location = context.Node.GetLocation();
            }

            ReportDiagnostic(context, Descriptor, location, expression);
        }

        /// <summary>
        /// Analyzes the 'new SQL command' node.
        /// </summary>
        /// <param name="context">The context.</param>
        private void AnalyzeNodeNewSqlCommand(SyntaxNodeAnalysisContext context) {
            var expression = context.Node as ObjectCreationExpressionSyntax;

            if (expression == null ||
                IsAnalysisSuppressed(expression, DiagnosticId) ||
                IsAnalysisSuppressed(expression, "CA2100")) {
                return;
            }

            Location location = null;

            if (EvaluateNodeNewSqlCommandObjectCreationExpression(ref location, context.SemanticModel, expression) != RuleViolationSeverityEnum.Error) {
                return;
            }

            if (location == null) {
                location = context.Node.GetLocation();
            }

            ReportDiagnostic(context, Descriptor, location, expression);
        }

        /// <summary>
        /// Evaluates the node 'command text expression statement'.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="node">The node.</param>
        protected RuleViolationSeverityEnum EvaluateNodeCommandTextExpressionStatement(
            ref Location location,
            SemanticModel semanticModel,
            ExpressionStatementSyntax node) {
            // Exit early if it's not actually an 'assignment' expression.
            var assignmentExpression = node.Expression as AssignmentExpressionSyntax;

            if (assignmentExpression == null) {
                return RuleViolationSeverityEnum.None;
            }

            // Exit early if the assignment expression is not for something called 'CommandText'.
            var memberAccessExpression = assignmentExpression.Left as MemberAccessExpressionSyntax;

            if (memberAccessExpression?.Name.Identifier.Text != "CommandText") {
                return RuleViolationSeverityEnum.None;
            }

            // Get detailed information regarding the property being accessed.
            var propertySymbol = semanticModel.GetSymbolInfo(memberAccessExpression).Symbol as IPropertySymbol;
                      
            if (propertySymbol == null) {
                return RuleViolationSeverityEnum.None;
            }

            // If the current property is not blacklisted.
            string propertySymbolString = propertySymbol.ToString();
            if (!SQLInjectionBlacklists.Properties.Any(value => propertySymbolString.Equals(value.Item2))) {
                return RuleViolationSeverityEnum.None;
            }

            // If CommandType != "Text" (the default), we can ignore
            var symbolName = (UnwrapParenthesizedExpressionDescending(memberAccessExpression.Expression) as IdentifierNameSyntax)?.Identifier.Text;
            if (IsCommandTypePropertyModifiedFromDefault(node.Expression, symbolName)) {
                return RuleViolationSeverityEnum.None;
            }

            // Determine if the data being assigned to the property is immutable.
            return IsDataImmutable(ref location, semanticModel, assignmentExpression.Right)
                ? RuleViolationSeverityEnum.None
                : RuleViolationSeverityEnum.Error;
        }

        /// <summary>
        /// Evaluates the node 'database SQL query'.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="node">The node.</param>
        /// Note:
        ///     It is assumed that methods invoking SQL commands follow the existing guidelines
        ///     and have the command to be executed as their first paramter.
        protected RuleViolationSeverityEnum EvaluateNodeDatabaseSqlQuery(
            ref Location location,
            SemanticModel semanticModel,
            InvocationExpressionSyntax node) {
            // Exit early if it's not actually a 'member access' expression, or is not some form of 'SqlQuery'.
            var expression = node.Expression as MemberAccessExpressionSyntax;

            if (expression == null) {
                return RuleViolationSeverityEnum.None;
            }

            // If the method being invoked is not blacklisted (basic check).
            if (!SQLInjectionBlacklists.Methods.Any(value => expression.Name.ToString().StartsWith(value.Item1))) {
                return RuleViolationSeverityEnum.None;
            }

            // Get detailed information regarding the method being invoked.
            var methodSymbol = semanticModel.GetSymbolInfo(expression).Symbol as IMethodSymbol;

            // Exit early if no method is found.
            if (methodSymbol == null) {
                return RuleViolationSeverityEnum.None;
            }

            // If the method being invoked is not blacklisted (detailed check).
            // Or no arguments are passed to the method.
            if (!SQLInjectionBlacklists.Methods.Any(value => methodSymbol.ConstructedFrom.ToString().StartsWith(value.Item2)) ||
                !node.ArgumentList.Arguments.Any()) {
                return RuleViolationSeverityEnum.None;
            }

            // Determine if the method's first argument contains immutable data.
            return IsDataImmutable(ref location, semanticModel, node.ArgumentList.Arguments.First().Expression)
                ? RuleViolationSeverityEnum.None
                : RuleViolationSeverityEnum.Error;
        }

        /// <summary>
        /// Evaluates the node 'new SQL command object creation expression'.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="node">The node.</param>
        protected RuleViolationSeverityEnum EvaluateNodeNewSqlCommandObjectCreationExpression(
            ref Location location,
            SemanticModel semanticModel,
            ObjectCreationExpressionSyntax node) {

            string symbolName;

            // Exit early if the node is not creating a new SqlCommand.
            if (!node.ToString().StartsWith("new SqlCommand")) {
                return RuleViolationSeverityEnum.None;
            }

            // If there were no arguments passed to the creation of the object...
            if (node.ArgumentList == null || node.ArgumentList.Arguments.Count == 0) {
                // ...determine if the object was created using the object initializer syntax.
                var initializationExpression = node.ChildNodes().OfType<InitializerExpressionSyntax>().FirstOrDefault();

                // If no, objected was created using a parameterless constructor and is not vulnerable to injection at this code point.
                if (initializationExpression == null) {
                    return RuleViolationSeverityEnum.None;
                }


                // Iterate through all child nodes that are assignment exressions.
                // Expressions can be in any order. If we find a non-Text CommandType, exit early
                AssignmentExpressionSyntax commandTextAssignmentChildNode = null;
                foreach (var assignmentChildNode in initializationExpression.ChildNodes().OfType<AssignmentExpressionSyntax>()) {
                    if (string.Equals(((IdentifierNameSyntax)(assignmentChildNode.Left)).Identifier.Text, "CommandText")) {
                        commandTextAssignmentChildNode = assignmentChildNode;
                        continue;
                    } else if (string.Equals(((IdentifierNameSyntax)(assignmentChildNode.Left)).Identifier.Text, "CommandType") &&
                        IsCommandTypeAssignmentExpressionNotText(assignmentChildNode)) {
                        return RuleViolationSeverityEnum.None;
                    }
                }

                // Determine if the data being assigned to the 'CommandText' property is immutable.
                return commandTextAssignmentChildNode == null || IsDataImmutable(ref location, semanticModel, commandTextAssignmentChildNode.Right)
                    ? RuleViolationSeverityEnum.None
                    : RuleViolationSeverityEnum.Error;                
            }

            // If CommandType != "Text" (the default), we can ignore
            symbolName = (UnwrapParenthesizedExpressionDescending(node.GetAncestorOfType<VariableDeclaratorSyntax>()) as VariableDeclaratorSyntax)?.Identifier.Text;
            if (IsCommandTypePropertyModifiedFromDefault(node, symbolName)) {
                return RuleViolationSeverityEnum.None;
            }

            // Determine if the first argument is immutable.
            return IsDataImmutable(ref location, semanticModel, node.ArgumentList.Arguments.First().Expression)
                ? RuleViolationSeverityEnum.None
                : RuleViolationSeverityEnum.Error;
        }

        /// <summary>
        /// Evaluates whether the commandSymbol's CommandType property has been modified
        /// from its default value of CommandType.Text
        /// </summary>
        /// <param name="node">The node from which to begin our evaluation</param>
        /// <param name="symbolName">The command symbol of the SqlCommand which we are interrogating</param>
        /// <returns>A bool representing whether the CommandType has been modified to be anything other than CommandType.Text</returns>
        private bool IsCommandTypePropertyModifiedFromDefault(ExpressionSyntax node, string symbolName) {
            // Get the method declaration ancestor of this node
            var methodDeclarationSyntax = node.GetAncestorOfType<MethodDeclarationSyntax>();
            if (methodDeclarationSyntax == null) {
                return false; // can't do anything here
            }

            var memberAccessExpressions = new List<MemberAccessExpressionSyntax>();

            // Find all member access expressions in this method
            GetExpressionsFromChildNodes<MemberAccessExpressionSyntax>(ref memberAccessExpressions, methodDeclarationSyntax);

            for (int i = 0; i < memberAccessExpressions.Count; ++i) {
                // Unwrap each one
                var unwrappedNode = UnwrapParenthesizedExpressionDescending(memberAccessExpressions[i]) as MemberAccessExpressionSyntax;

                // Only interested in assignments to the CommandType property of the commandSymbol object in question, eg. command.CommandType[ = X]
                if (unwrappedNode?.Expression == null ||
                    !(unwrappedNode.Expression is IdentifierNameSyntax) ||
                    unwrappedNode.Name == null ||
                    ((IdentifierNameSyntax)unwrappedNode.Expression).Identifier.Text != symbolName ||
                    !string.Equals(unwrappedNode.Name.Identifier.Text, "CommandType")) {
                    continue;
                }

                // Get the greater assignment expression
                var assignmentExpression = unwrappedNode.GetAncestorOfType<AssignmentExpressionSyntax>();
                if (assignmentExpression == null) {
                    continue;
                }

                if (IsCommandTypeAssignmentExpressionNotText(assignmentExpression)) {
                    return true; // Exit early
                }
            }
            return false;
        }

        private bool IsCommandTypeAssignmentExpressionNotText(AssignmentExpressionSyntax assignmentExpression) {
            var expression = UnwrapParenthesizedExpressionDescending(assignmentExpression.Right) as MemberAccessExpressionSyntax;
            // Nullcheck and such
            return (expression != null &&
                !string.Equals(expression.GetLastToken().Text, "Text"));
        }

        /// <summary>
        /// Evaluates the specified variable, traversing the syntax tree to find data assignments,
        /// recursively iterating through the child objects of the data being assigned
        /// to determine if all data assigned to the variable is immutable.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="variable">The variable.</param>
        /// <param name="parentDeclaration">The parent declaration.</param>
        private static RuleViolationSeverityEnum EvaluateVariable(
            ref Location location,
            SemanticModel semanticModel,
            SimpleNameSyntax variable,
            MemberDeclarationSyntax parentDeclaration = null) {
            if (IsExpressionNodeConstantString(semanticModel, variable)) {
                return RuleViolationSeverityEnum.None;
            }

            MemberDeclarationSyntax memberDeclaration;

            // If the parent declaration is not null, use the existing declaration.
            if (parentDeclaration != null) {
                memberDeclaration = parentDeclaration;
            } else {
                // Otherwise, get the parent method or property declaration.
                memberDeclaration = variable.GetAncestorOfType<BaseMethodDeclarationSyntax>();

                if (memberDeclaration == null) {
                    memberDeclaration = variable.GetAncestorOfType<BasePropertyDeclarationSyntax>();

                    // If the node is neither within a method nor a property, use case is out of scope.
                    if (memberDeclaration == null) {
                        return RuleViolationSeverityEnum.Error;
                    }
                }
            }

            // Get the latest data assignments for the variable.
            // NOTE:
            //      string foo = "foo";    <--- EqualsValueClauseSyntax
            //      foo = "foo";           <--- AssignmentExpressionSyntax
            //
            //      These two 'assignments' are viewed differently by the Roslyn engine,
            //      and as such must be handled separately.
            EqualsValueClauseSyntax latestEqualsValueExpression;
            AssignmentExpressionSyntax latestAssignmentExpression;

            // Iterates through all assignment expressions (of both types) returning only the most recent of each.
            GetLatestDataAssignmentExpressions(
                variable,
                memberDeclaration,
                out latestEqualsValueExpression,
                out latestAssignmentExpression);

            // Searches the syntax tree to determine which of the two 'latest' results is actually the most recent,
            // then retrieves the child nodes from that object.
            IEnumerable<SyntaxNode> latestChildren = GetDataAssignmentLatestChildNodes(
                variable,
                memberDeclaration,
                latestEqualsValueExpression,
                latestAssignmentExpression);

            // If no children were retrieved, then no data was assigned to the variable.
            if (latestChildren == null) {
                // Variable may be a parameter from a method. Attempt to evaluate it as such.
                var methodDeclaration = memberDeclaration as BaseMethodDeclarationSyntax;

                return methodDeclaration == null
                    ? RuleViolationSeverityEnum.Error
                    : EvaluateVariableAsMethodParameter(ref location, semanticModel, methodDeclaration, variable);
            }

            // Iterate through each of the data nodes being assigned to the variable.
            foreach (var childNode in latestChildren) {
                IdentifierNameSyntax childVariable = childNode as IdentifierNameSyntax;

                // If the data being assigned to the target variable, is also a variable...
                if (childVariable != null) {
                    // ...recursively repeat this process for that variable, moving up the syntax tree.
                    if (EvaluateVariable(ref location, semanticModel, childVariable, memberDeclaration) == RuleViolationSeverityEnum.Error) {
                        return RuleViolationSeverityEnum.Error;
                    }
                } else {
                    // ...otherwise determine if the data is immutable.
                    if (!IsDataImmutable(ref location, semanticModel, childNode)) {
                        return RuleViolationSeverityEnum.Error;
                    }
                }
            }

            return RuleViolationSeverityEnum.None;
        }

        /// <summary>
        /// Attempts to evaluate the specified variable as a method parameter.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="method">The method.</param>
        /// <param name="variable">The variable.</param>
        private static RuleViolationSeverityEnum EvaluateVariableAsMethodParameter(
            ref Location location,
            SemanticModel semanticModel,
            BaseMethodDeclarationSyntax method,
            SyntaxNode variable) {
            // If the method is not private, the use case is out of scope for analysis,
            // as such it must be assumed that the method is vulnerable.
            if (!method.Modifiers.Select(token => token.Text).Contains("private", StringComparer.OrdinalIgnoreCase)) {
                return RuleViolationSeverityEnum.Error;
            }

            // If the method does not contain the specified variable as a parameter, there is no violation.
            if (method.ParameterList.Parameters.All(parameterSyntax => !string.Equals(variable.ToString(), parameterSyntax.Identifier.Text))) {
                return RuleViolationSeverityEnum.None;
            }

            // Find the parent class for the method.
            var parentClass = method.GetAncestorOfType<ClassDeclarationSyntax>();

            // If no parent class exists, exit early with an error.
            if (parentClass == null) {
                return RuleViolationSeverityEnum.Error;
            }

            // If the parent class is a 'partial' class, the use case is out of scope for analysis,
            // as such it must be assumed that the method is vulnerable.
            if (parentClass.Modifiers.Select(token => token.Text).Contains("partial", StringComparer.OrdinalIgnoreCase)) {
                return RuleViolationSeverityEnum.Error;
            }

            // Get all method invocations in the specified class.
            var methodInvocations = new List<InvocationExpressionSyntax>(DefaultCapacity * 2);

            GetExpressionsFromChildNodes(ref methodInvocations, parentClass);

            // If the class contains no method invocations, exit with no error.
            if (!methodInvocations.Any()) {
                return RuleViolationSeverityEnum.None;
            }

            // Get detailed symbol info about the declared method.
            var methodDeclarationType = semanticModel.GetDeclaredSymbol(method);

            // If no symbol was found, exit with no error.
            if (methodDeclarationType == null) {
                return RuleViolationSeverityEnum.None;
            }

            // Iterate through all method invocations within the class, where the type of the method matches the declared method's type.
            foreach (var methodInvocation in methodInvocations.Where(
                invocationExpression => Equals(methodDeclarationType, semanticModel.GetSymbolInfo(invocationExpression).Symbol as IMethodSymbol))) {
                // If the discovered method does not have any arguments, ignore it.
                if (methodInvocation.ArgumentList == null || !methodInvocation.ArgumentList.Arguments.Any()) {
                    continue;
                }

                // Evaluate the method's arguments.
                foreach (var argument in methodInvocation.ArgumentList.Arguments) {
                    // If the argument type is not a System.String, ignore it.
                    if (!semanticModel.GetTypeInfo(argument.Expression).Type
                            .ContainingNamespace
                            .ToDisplayString()
                            .Equals("System", StringComparison.OrdinalIgnoreCase) ||
                        !semanticModel.GetTypeInfo(argument.Expression).Type
                            .Name
                            .Equals("String", StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }

                    // Determine if the argument's data is immutable.
                    if (IsDataImmutable(ref location, semanticModel, argument)) {
                        continue;
                    }

                    // If the data is not immutable, update the diagnostic location to be that of the method's argument.
                    location = argument.GetLocation();

                    // Raise an error.
                    return RuleViolationSeverityEnum.Error;
                }
            }

            return RuleViolationSeverityEnum.None;
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
                ? equalsValueExpressions.LastOrDefault(syntax => syntax.Parent.ToString().StartsWith(variable.Identifier.ToString()))
                : null;

            // If results were returned, grab the latest.
            latestAssignmentExpression = assignmentExpressions.Any()
                ? assignmentExpressions.LastOrDefault(syntax => syntax.Left.ToString().StartsWith(variable.Identifier.ToString()))
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
                var allExpressions = new List<SyntaxNode>(DefaultCapacity * 5);

                // Gets *all* child expressions, in 'top to bottom' order, stopping at the target node.
                GetAllExpressionsFromChildNodes(ref allExpressions, node, variable);

                // Reverses the results, such that the most recent expressions appear first.
                allExpressions.Reverse();

                // Iterate through the result expressions, finding either of the two assignment expressions.
                // Return the first result found.
                foreach (var expression in allExpressions) {
                    if (expression.Equals(latestAssignmentExpression)) {
                        return latestAssignmentExpression
                            .ChildNodes()
                            .Where(syntax => !syntax.Equals(latestAssignmentExpression.Left));
                    }

                    if (!expression.Equals(latestEqualsValueExpression)) {
                        continue;
                    }

                    return latestEqualsValueExpression.ChildNodes();
                }
            } else if (latestEqualsValueExpression != null) {
                return latestEqualsValueExpression
                    .ChildNodes();
            } else if (latestAssignmentExpression != null) {
                return latestAssignmentExpression
                    .ChildNodes()
                    .Where(syntax => !syntax.Equals(latestAssignmentExpression.Left));
            }

            return null;
        }

        /// <summary>
        /// Determines whether the specified node is immutable data.
        /// </summary>
        /// <param name="location">The location.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="node">The node.</param>
        /// <param name="parentDeclaration">The parent declaration.</param>
        private static bool IsDataImmutable(
            ref Location location,
            SemanticModel semanticModel,
            SyntaxNode node,
            MemberDeclarationSyntax parentDeclaration = null) {
            // If the node is a method invocation, fail and exit early.
            if (node is InvocationExpressionSyntax) {
                return false;
            }

            MemberDeclarationSyntax memberDeclaration;

            // If the parent declaration is not null, use the existing declaration.
            if (parentDeclaration != null) {
                memberDeclaration = parentDeclaration;
            } else {
                // If the node is neither within a method nor a property, use case is out of scope.
                memberDeclaration = node.GetAncestorOfType<BaseMethodDeclarationSyntax>();

                if (memberDeclaration == null) {
                    memberDeclaration = node.GetAncestorOfType<BasePropertyDeclarationSyntax>();

                    // If the node is neither within a method nor a property, use case is out of scope.
                    if (memberDeclaration == null) {
                        return true;
                    }
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
                var addExpression = childNode as BinaryExpressionSyntax;

                if (addExpression != null) {
                    // ...ensure it's an 'add' expression.
                    if (addExpression.Kind() != SyntaxKind.AddExpression) {
                        return false;
                    }

                    // Recursively determine if both sides of the expression contain immutable data.
                    if (!IsDataImmutable(ref location, semanticModel, addExpression.Left, memberDeclaration) ||
                        !IsDataImmutable(ref location, semanticModel, addExpression.Right, memberDeclaration)) {
                        return false;
                    }

                    continue;
                }

                // If the parent node is a conditional expression...
                var conditionalExpression = node as ConditionalExpressionSyntax;

                // ...and the parent node's 'Condition' node is the current child node...
                if (conditionalExpression != null && conditionalExpression.Condition == childNode) {
                    continue;
                }

                // If the parent node is a MemberAccessExpression...
                var memberAccessExpression = node as MemberAccessExpressionSyntax;

                if (memberAccessExpression != null) {
                    var expression = memberAccessExpression.Expression as IdentifierNameSyntax;
                    var name = memberAccessExpression.Name as IdentifierNameSyntax;

                    if (expression != null && name != null) {
                        var propertySymbol = semanticModel.GetSymbolInfo(name).Symbol as IFieldSymbol;

                        if (propertySymbol?.IsConst == true ||
                            propertySymbol?.IsStatic == true && propertySymbol.IsReadOnly) {
                            return IsDataImmutable(ref location, semanticModel, name, memberDeclaration);
                        }
                    }
                }

                // If the child node is a variable...
                IdentifierNameSyntax identifierName = childNode as IdentifierNameSyntax;

                if (identifierName != null) {
                    // Traverse the syntax tree to determine if the data assigned to the variable is immutable.
                    if (EvaluateVariable(ref location, semanticModel, identifierName, memberDeclaration) != RuleViolationSeverityEnum.Error) {
                        continue;
                    }

                    return false;
                }

                // If the child node is also a conditional expression...
                conditionalExpression = childNode as ConditionalExpressionSyntax;

                // Evaluate both the WhenTrue and WhenFalse expressions.
                if (conditionalExpression == null ||
                    !IsDataImmutable(ref location, semanticModel, conditionalExpression.WhenTrue, memberDeclaration) ||
                    !IsDataImmutable(ref location, semanticModel, conditionalExpression.WhenFalse, memberDeclaration)) {
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
                return localSymbol.IsConst &&
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

            return (fieldSymbol.IsConst || (fieldSymbol.IsStatic && fieldSymbol.IsReadOnly)) &&
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
                if (IsImmutableFieldOrProperty(semanticModel, memberAccessExpression)) {
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
                ? IsImmutableFieldOrProperty(semanticModel, memberAccessExpression)
                : SQLInjectionWhitelists.Properties.Any(value => typeSymbol.OriginalDefinition.ToString().Contains(value.Item2));
        }

        /// <summary>
        /// Determines whether the specified member access expression references an immutable property or field.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="memberAccessExpression">The member access expression.</param>
        private static bool IsImmutableFieldOrProperty(
            SemanticModel semanticModel,
            MemberAccessExpressionSyntax memberAccessExpression) {
            if (memberAccessExpression == null) {
                return false;
            }

            var symbol = semanticModel
                .GetSymbolInfo(memberAccessExpression)
                .Symbol;

            if (symbol == null) {
                return false;
            }

            var propertySymbol = symbol as IPropertySymbol;

            if (propertySymbol != null) {
                return propertySymbol.IsReadOnly && propertySymbol.IsStatic;
            }

            var fieldSymbol = symbol as IFieldSymbol;

            return fieldSymbol != null &&
                   (fieldSymbol.IsConst || fieldSymbol.IsReadOnly && fieldSymbol.IsStatic);
        }
    }
}
