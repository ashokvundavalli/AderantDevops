using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Analyzer.Exclusions;
using Aderant.Build.Analyzer.SQLInjection.Lists;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {
    internal abstract class SqlInjectionRuleBase : RuleBase {
        private const int DefaultCapacity = 25;

        protected enum SqlInjectionRuleViolationSeverityEnum {
            Invalid = -1,
            None,
            Info,
            Warning,
            Error,
            Max
        }

        private bool? ignoreProject;

        /// <summary>
        /// Evaluates the node 'command text expression statement'.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="node">The node.</param>
        protected SqlInjectionRuleViolationSeverityEnum EvaluateNodeCommandTextExpressionStatement(
            SemanticModel semanticModel,
            ExpressionStatementSyntax node) {
            // Exit early if it's not actually an 'assignment' expression.
            var assignmentExpression = node.Expression as AssignmentExpressionSyntax;

            if (assignmentExpression == null) {
                return SqlInjectionRuleViolationSeverityEnum.None;
            }

            // Exit early if the assignment expression is not for something called 'CommandText'.
            var memberAccessExpression = assignmentExpression.Left as MemberAccessExpressionSyntax;

            if (memberAccessExpression?.Name.ToString() != "CommandText") {
                return SqlInjectionRuleViolationSeverityEnum.None;
            }

            // Get detailed information regarding the property being accessed.
            var propertySymbol = ModelExtensions.GetSymbolInfo(semanticModel, memberAccessExpression).Symbol as IPropertySymbol;

            // Ensure that the 'CommandText' is actually for some form of SQL command, and not something else.
            return propertySymbol != null &&
                   SQLInjectionBlacklists.Properties.Any(value => propertySymbol.ToString().Equals(value.Item2)) &&
                   EvaluateExpressionTree(semanticModel, assignmentExpression.Right) != SqlInjectionRuleViolationSeverityEnum.None
                ? EvaluateMethodForSqlParameters(semanticModel, node)
                : SqlInjectionRuleViolationSeverityEnum.None;
        }

        /// <summary>
        /// Evaluates the node 'database SQL query'.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="node">The node.</param>
        protected SqlInjectionRuleViolationSeverityEnum EvaluateNodeDatabaseSqlQuery(
            SemanticModel semanticModel,
            InvocationExpressionSyntax node) {
            // Exit early if it's not actually a 'member access' expression, or is not some form of 'SqlQuery'.
            var expression = node.Expression as MemberAccessExpressionSyntax;

            if (expression == null) {
                return SqlInjectionRuleViolationSeverityEnum.None;
            }

            if (!SQLInjectionBlacklists.Methods.Any(value => expression.Name.ToString().StartsWith(value.Item1))) {
                return SqlInjectionRuleViolationSeverityEnum.None;
            }

            // Get detailed information regarding the method being invoked.
            var methodSymbol = semanticModel.GetSymbolInfo(expression).Symbol as IMethodSymbol;

            // Exit early if no method is found.
            if (methodSymbol == null) {
                return SqlInjectionRuleViolationSeverityEnum.None;
            }

            // Exit early if it's not the correct method.
            // Attempt to examine the first argument (SQL Query Text) to determine the severity of the violation.
            return SQLInjectionBlacklists.Methods.Any(value => methodSymbol.ConstructedFrom.ToString().StartsWith(value.Item2)) &&
                   node.ArgumentList.Arguments.Any()
                ? EvaluateExpressionTree(semanticModel, node.ArgumentList.Arguments.First().Expression)
                : SqlInjectionRuleViolationSeverityEnum.None;
        }

        /// <summary>
        /// Evaluates the node 'new SQL command object creation expression'.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="node">The node.</param>
        protected SqlInjectionRuleViolationSeverityEnum EvaluateNodeNewSqlCommandObjectCreationExpression(
            SemanticModel semanticModel,
            ObjectCreationExpressionSyntax node) {
            // Are we trying to operate on something that's not a 'new SqlCommand'?
            return !node.ToString().StartsWith("new SqlCommand")
                // If yes, exit early.
                ? SqlInjectionRuleViolationSeverityEnum.None
                // Otherwise, determine if we're passing arguments.
                : node.ArgumentList.Arguments.Any()
                    // If yes, grab the first argument and determine the severity of the violation.
                    ? EvaluateExpressionTree(semanticModel, node.ArgumentList.Arguments.First().Expression)
                    : SqlInjectionRuleViolationSeverityEnum.None;
        }

        /// <summary>
        /// Uses recursion to evaluate the specified expression tree for string literal and constant string values.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="expression">The expression.</param>
        /// <returns>
        /// A SqlInjectionRuleViolationSeverityEnum value specifiying the severity level of the rule violation.
        /// </returns>
        private static SqlInjectionRuleViolationSeverityEnum EvaluateExpressionTree(
            SemanticModel semanticModel,
            ExpressionSyntax expression) {
            // Is the provided expression a binary expression? E.g. A + B or C * D.
            var binaryExpressionSyntax = expression as BinaryExpressionSyntax;

            // If no, is it a conditional 'X ? Y : Z' expression?
            if (binaryExpressionSyntax == null) {
                var conditionalExpression = expression as ConditionalExpressionSyntax;

                // If no, cease attempting to navigate the 'tree' and just evaluate the expression.
                if (conditionalExpression == null) {
                    return EvaluateExpressionRuleViolationSeverity(semanticModel, expression);
                }

                // If yes, recursively examine the 'WhenTrue' and 'WhenFalse' expressions.
                return EvaluateExpressionTree(semanticModel, conditionalExpression.WhenTrue) ==
                       SqlInjectionRuleViolationSeverityEnum.Error
                    ? SqlInjectionRuleViolationSeverityEnum.Error
                    : EvaluateExpressionTree(semanticModel, conditionalExpression.WhenFalse);
            }

            // If it is a binary expression, but is not an Add expression,
            // there is no use-case where this is acceptable, so default to an error.
            if (binaryExpressionSyntax.Kind() != SyntaxKind.AddExpression) {
                return SqlInjectionRuleViolationSeverityEnum.Error;
            }

            // Begin navigating the 'tree', starting with the 'left' side. E.g. 'X' in the expression: X + Y.
            // Is the 'left' side also a binary expression?
            var left = binaryExpressionSyntax.Left as BinaryExpressionSyntax;

            if (left == null) {
                // If no, cease navigating that side of the 'tree' and just evaluate the expression.
                SqlInjectionRuleViolationSeverityEnum validity =
                    EvaluateExpressionRuleViolationSeverity(semanticModel, binaryExpressionSyntax.Left);

                // If the result is an 'Error' severity, exit early as this is the highest state of severity.
                if (validity == SqlInjectionRuleViolationSeverityEnum.Error) {
                    return SqlInjectionRuleViolationSeverityEnum.Error;
                }
            } else if (left.Kind() != SyntaxKind.AddExpression) {
                // If it is a binary expression, but is not an Add expression,
                // there is no use-case where this is acceptable, so default to an error.
                return SqlInjectionRuleViolationSeverityEnum.Error;
            } else {
                // Otherwise, if the 'left' is a valid binary expression, use recursion to further traverse the 'tree'.
                SqlInjectionRuleViolationSeverityEnum validity = EvaluateExpressionTree(semanticModel, left);

                // If the result is an 'Error' severity, exit early as this is the highest state of severity.
                if (validity == SqlInjectionRuleViolationSeverityEnum.Error) {
                    return SqlInjectionRuleViolationSeverityEnum.Error;
                }
            }

            // Repeat the process with the 'right' side of the 'tree'. E.g. 'Y' in the expression: X + Y.
            // Is the 'right' side also a binary expression?
            var right = binaryExpressionSyntax.Right as BinaryExpressionSyntax;

            if (right == null) {
                // If no, cease navigating that side of the 'tree' and just evaluate the expression.
                SqlInjectionRuleViolationSeverityEnum validity =
                    EvaluateExpressionRuleViolationSeverity(semanticModel, binaryExpressionSyntax.Right);

                // If the result is an 'Error' severity, exit early as this is the highest state of severity.
                if (validity == SqlInjectionRuleViolationSeverityEnum.Error) {
                    return SqlInjectionRuleViolationSeverityEnum.Error;
                }
            } else if (right.Kind() != SyntaxKind.AddExpression) {
                // If it is a binary expression, but is not an Add expression,
                // there is no use-case where this is acceptable, so default to an error.
                return SqlInjectionRuleViolationSeverityEnum.Error;
            } else {
                // Otherwise, if the 'right' is a valid binary expression, use recursion to further traverse the 'tree'.
                SqlInjectionRuleViolationSeverityEnum validity = EvaluateExpressionTree(semanticModel, right);

                // If the result is an 'Error' severity, exit early as this is the highest state of severity.
                if (validity == SqlInjectionRuleViolationSeverityEnum.Error) {
                    return SqlInjectionRuleViolationSeverityEnum.Error;
                }
            }

            // If we made it all the way through, there is no violation.
            return SqlInjectionRuleViolationSeverityEnum.None;
        }

        /// <summary>
        /// Evaluates the rule violation severity of the expression.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="expression">The expression.</param>
        private static SqlInjectionRuleViolationSeverityEnum EvaluateExpressionRuleViolationSeverity(
            SemanticModel semanticModel,
            ExpressionSyntax expression) {
            // If the expression is a string literal, there is no violation.
            if (expression is LiteralExpressionSyntax) {
                return SqlInjectionRuleViolationSeverityEnum.None;
            }

            // If the expression is a property accessor contained within the whitelist.
            var memberAccessExpression = expression as MemberAccessExpressionSyntax;

            if (memberAccessExpression != null) {
                var typeSymbol = semanticModel.GetSymbolInfo(memberAccessExpression.Expression).Symbol as INamedTypeSymbol;

                return typeSymbol == null ||
                    SQLInjectionWhitelists.Properties.Any(value => typeSymbol.OriginalDefinition.ToString().Contains(value.Item2))
                    ? SqlInjectionRuleViolationSeverityEnum.None
                    : SqlInjectionRuleViolationSeverityEnum.Error;
            }

            // If the expression is not a variable of some description,
            // there is no use-case where this is acceptable, so default to an error.
            if (!(expression is IdentifierNameSyntax)) {
                return SqlInjectionRuleViolationSeverityEnum.Error;
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
                       localSymbol.Type.ContainingNamespace.ToString().Equals("System")
                    ? SqlInjectionRuleViolationSeverityEnum.None
                    : SqlInjectionRuleViolationSeverityEnum.Error;
            }

            var fieldSymbol = symbol as IFieldSymbol;

            // If the expression is a class-level field,
            // Return a severity of 'None' if the field is a constant string from the 'System' namespace.
            // Otherwise return a severity of 'Error'.
            if (fieldSymbol == null) {
                return SqlInjectionRuleViolationSeverityEnum.Error;
            }

            return fieldSymbol.IsConst &&
                   fieldSymbol.Type.ToString().Equals("string") &&
                   fieldSymbol.Type.ContainingNamespace.ToString().Equals("System")
                ? SqlInjectionRuleViolationSeverityEnum.None
                : SqlInjectionRuleViolationSeverityEnum.Error;
        }

        /// <summary>
        /// Examines the ancestor (parent) nodes for the provided syntax node and locates the parent method definition expression.
        /// Then recursively iterates through every 'child' node within that method definition searching for usages of SqlCommand Parameters.
        /// A syntax node is a section of code, such as a method invocation, variable assignment, or method definition; to name a few examples.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="node">The node.</param>
        private static SqlInjectionRuleViolationSeverityEnum EvaluateMethodForSqlParameters(SemanticModel semanticModel, SyntaxNode node) {
            MethodDeclarationSyntax methodDeclaration = null;

            foreach (SyntaxNode ancestorNode in node.Ancestors()) {
                methodDeclaration = ancestorNode as MethodDeclarationSyntax;

                if (methodDeclaration != null) {
                    break;
                }
            }

            if (methodDeclaration == null) {
                return SqlInjectionRuleViolationSeverityEnum.Error;
            }

            IMethodSymbol parentMethodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration);

            if (parentMethodSymbol == null ||
                !SQLInjectionWhitelists.Methods.Any(value => parentMethodSymbol.OriginalDefinition.ToString().Equals(value.Item2))) {
                return SqlInjectionRuleViolationSeverityEnum.Error;
            }

            // The default capacity was chosen to reduce the impact of multiple instantiations of the array during the below recursive iteration.
            var invocationExpressionList = new List<InvocationExpressionSyntax>(DefaultCapacity);

            // This is a recursive method that iterates through every child node and retrieves the (method) invocation expressions.
            GetInvocationExpressionsFromChildNodes(ref invocationExpressionList, methodDeclaration);

            foreach (InvocationExpressionSyntax invocationExpression in invocationExpressionList) {
                var memberAccessExpression = invocationExpression.Expression as MemberAccessExpressionSyntax;

                if (memberAccessExpression == null ||
                    !memberAccessExpression.Name.ToString().StartsWith("Add")) {
                    continue;
                }

                var methodSymbol = semanticModel.GetSymbolInfo(memberAccessExpression).Symbol as IMethodSymbol;

                if (methodSymbol == null ||
                    methodSymbol.ToString().Equals("System.Data.SqlClient.SqlParameterCollection.Add(System.Data.SqlClient.SqlParameter)") ||
                    methodSymbol.ToString().Equals("System.Data.SqlClient.SqlParameterCollection.AddRange(System.Data.SqlClient.SqlParameter[])")) {
                    // Returns a warning, rather than no error.
                    return SqlInjectionRuleViolationSeverityEnum.Warning;
                }
            }

            return SqlInjectionRuleViolationSeverityEnum.Error;
        }

        /// <summary>
        /// Recursively iterates through all child nodes, adding any (method) invocation expressions to the referenced list.
        /// </summary>
        /// <param name="invocationExpressionList">The invocation expression list.</param>
        /// <param name="node">The node.</param>
        private static void GetInvocationExpressionsFromChildNodes(
            ref List<InvocationExpressionSyntax> invocationExpressionList,
            SyntaxNode node) {
            foreach (var childNode in node.ChildNodes()) {
                var invocationExpression = childNode as InvocationExpressionSyntax;

                if (invocationExpression == null) {
                    // Recursion.
                    GetInvocationExpressionsFromChildNodes(ref invocationExpressionList, childNode);
                } else {
                    invocationExpressionList.Add(invocationExpression);
                }
            }
        }

        /// <summary>
        /// Examines the specified context to determine if the method containing
        /// the targeted node includes a code analysis suppression attribute.
        /// </summary>
        /// <param name="context">The context.</param>
        protected bool IsAnalysisSuppressed(SyntaxNodeAnalysisContext context) {
            MethodDeclarationSyntax methodDeclaration = null;

            // Climb the syntax tree looking for a method declaration.
            foreach (SyntaxNode ancestor in context.Node.Ancestors()) {
                methodDeclaration = ancestor as MethodDeclarationSyntax;

                if (methodDeclaration != null) {
                    break;
                }
            }

            if (methodDeclaration == null) {
                return false;
            }

            foreach (AttributeListSyntax attributeList in methodDeclaration.ChildNodes().OfType<AttributeListSyntax>()) {
                foreach (AttributeSyntax attribute in attributeList.Attributes) {
                    if (attribute.Name.ToString().Equals("ExcludeFromCodeCoverage")) {
                        return true;
                    }

                    if (!attribute.Name.ToString().Equals("SuppressMessage")) {
                        continue;
                    }

                    if (attribute.ArgumentList.Arguments[0].ToString().Equals("\"SQL Injection\"", StringComparison.OrdinalIgnoreCase) &&
                        attribute.ArgumentList.Arguments[1].ToString().StartsWith("\"Aderant_SqlInjection", StringComparison.OrdinalIgnoreCase)) {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// References the 'SqlInjectionExclusions.cs' list of excluded source locations
        /// and determines if the current project is to be excluded from rule evaluations.
        /// </summary>
        /// <param name="context">The context.</param>
        protected bool IsProjectIgnored(SyntaxNodeAnalysisContext context) {
            if (ignoreProject.HasValue) {
                return ignoreProject.Value;
            }

            string sourceLocation = context.Node.GetLocation().ToString();

            foreach (string exclusion in new SqlInjectionExclusions().ExclusionsList) {
                if (sourceLocation.Contains(exclusion)) {
                    ignoreProject = true;
                }
            }

            if (ignoreProject == true) {
                return ignoreProject.Value;
            }

            ignoreProject = false;

            return ignoreProject.Value;
        }
    }
}
