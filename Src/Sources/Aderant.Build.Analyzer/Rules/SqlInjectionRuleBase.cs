using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aderant.Build.Analyzer.Rules {
    public abstract class SqlInjectionRuleBase : RuleBase {
        protected enum SqlInjectionRuleViolationSeverityEnum {
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
        protected SqlInjectionRuleViolationSeverityEnum EvaluateNodeCommandTextExpressionStatement(
            SemanticModel semanticModel,
            ExpressionStatementSyntax node) {
            // Exit early if it's not actually an 'assignment' expression.
            AssignmentExpressionSyntax assignmentExpression = node.Expression as AssignmentExpressionSyntax;

            if (assignmentExpression == null) {
                return SqlInjectionRuleViolationSeverityEnum.None;
            }

            // Exit early if the assignment expression is not for something called 'CommandText'.
            MemberAccessExpressionSyntax memberAccessExpression = assignmentExpression.Left as MemberAccessExpressionSyntax;

            if (memberAccessExpression?.Name.ToString() != "CommandText") {
                return SqlInjectionRuleViolationSeverityEnum.None;
            }

            // Get detailed information regarding the property being accessed.
            IPropertySymbol propertySymbol = ModelExtensions.GetSymbolInfo(semanticModel, memberAccessExpression).Symbol as IPropertySymbol;

            // Ensure that the 'CommandText' is actually for some form of SQL command, and not something else.
            return (propertySymbol?.ToString().Equals("System.Data.Common.DbCommand.CommandText") ?? false) ||
                   (propertySymbol?.ToString().Equals("System.Data.IDbCommand.CommandText") ?? false) ||
                   (propertySymbol?.ToString().Equals("System.Data.SqlClient.SqlCommand.CommandText") ?? false)
                ? EvaluateExpressionTree(semanticModel, assignmentExpression.Right)
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
            // Exit early if it's not actually a 'member access' expression, or is not an 'SqlQuery'.
            MemberAccessExpressionSyntax expression = node.Expression as MemberAccessExpressionSyntax;

            if (expression == null ||
                !expression.Name.ToString().StartsWith("SqlQuery")) {
                return SqlInjectionRuleViolationSeverityEnum.None;
            }

            // Get detailed information regarding the method being invoked.
            IMethodSymbol methodSymbol = semanticModel.GetSymbolInfo(expression).Symbol as IMethodSymbol;

            // Exit early if it's not the correct method, or not a method at all.
            if (methodSymbol == null ||
                !methodSymbol.ConstructedFrom.ToString().StartsWith("System.Data.Entity.Database.SqlQuery<TElement>(string")) {
                return SqlInjectionRuleViolationSeverityEnum.None;
            }

            // Attempt to examine the first argument (SQL Query Text) to determine the severity of the violation.
            return node.ArgumentList.Arguments.Any()
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
        protected SqlInjectionRuleViolationSeverityEnum EvaluateExpressionTree(
            SemanticModel semanticModel,
            ExpressionSyntax expression) {
            // Is the provided expression a binary expression? E.g. A + B or C * D.
            BinaryExpressionSyntax binaryExpressionSyntax = expression as BinaryExpressionSyntax;

            // If no, cease attempting to navigate the 'tree' and simply evaluate the expression.
            if (binaryExpressionSyntax == null) {
                return EvaluateExpressionRuleViolationSeverity(semanticModel, expression);
            }

            // If it is a binary expression, but is not an Add expression,
            // there is no use-case where this is acceptable, so default to an error.
            if (binaryExpressionSyntax.Kind() != SyntaxKind.AddExpression) {
                return SqlInjectionRuleViolationSeverityEnum.Error;
            }

            // Begin navigating the 'tree', starting with the 'left' side. E.g. 'X' in the expression: X + Y.
            // Is the 'left' side also a binary expression?
            BinaryExpressionSyntax left = binaryExpressionSyntax.Left as BinaryExpressionSyntax;

            if (left == null) {
                // If no, cease navigating that side of the 'tree' and simply evaluate the expression.
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
                SqlInjectionRuleViolationSeverityEnum validity =
                    EvaluateExpressionTree(semanticModel, left);

                // If the result is an 'Error' severity, exit early as this is the highest state of severity.
                if (validity == SqlInjectionRuleViolationSeverityEnum.Error) {
                    return SqlInjectionRuleViolationSeverityEnum.Error;
                }
            }

            // Repeat the process with the 'right' side of the 'tree'. E.g. 'Y' in the expression: X + Y.
            // Is the 'right' side also a binary expression?
            BinaryExpressionSyntax right = binaryExpressionSyntax.Right as BinaryExpressionSyntax;

            if (right == null) {
                // If no, cease navigating that side of the 'tree' and simply evaluate the expression.
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
                SqlInjectionRuleViolationSeverityEnum validity =
                    EvaluateExpressionTree(semanticModel, right);

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
        protected SqlInjectionRuleViolationSeverityEnum EvaluateExpressionRuleViolationSeverity(
            SemanticModel semanticModel,
            ExpressionSyntax expression) {
            // If the expression is a string literal, there is no violation.
            if (expression is LiteralExpressionSyntax) {
                return SqlInjectionRuleViolationSeverityEnum.None;
            }

            // If the expression is not a variable of some description,
            // there is no use-case where this is acceptable, so default to an error.
            if (!(expression is IdentifierNameSyntax)) {
                return SqlInjectionRuleViolationSeverityEnum.Error;
            }

            // Get detailed information regarding the expression.
            ISymbol symbol = semanticModel.GetSymbolInfo(expression).Symbol;

            ILocalSymbol localSymbol = symbol as ILocalSymbol;

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

            IFieldSymbol fieldSymbol = symbol as IFieldSymbol;

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
    }
}
