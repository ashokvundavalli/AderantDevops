using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {
    public abstract class SqlInjectionRuleBase : RuleBase {
        /// <summary>
        /// Returns an assignment expression for DbCommand.CommandText.
        /// </summary>
        /// <param name="context">The context.</param>
        protected AssignmentExpressionSyntax GetCommandTextAssignmentExpression(SyntaxNodeAnalysisContext context) {
            ExpressionStatementSyntax invocationExpression = (ExpressionStatementSyntax)context.Node;

            AssignmentExpressionSyntax assignmentExpression = invocationExpression.Expression as AssignmentExpressionSyntax;

            if (assignmentExpression == null) {
                return null;
            }

            MemberAccessExpressionSyntax memberAccessExpression = assignmentExpression.Left as MemberAccessExpressionSyntax;

            if (memberAccessExpression?.Name.ToString() != "CommandText") {
                return null;
            }

            IPropertySymbol propertySymbol = ModelExtensions.GetSymbolInfo(context.SemanticModel, memberAccessExpression).Symbol as IPropertySymbol;

            return (propertySymbol?.ToString().Equals("System.Data.Common.DbCommand.CommandText") ?? false) ||
                   (propertySymbol?.ToString().Equals("System.Data.IDbCommand.CommandText") ?? false) ||
                   (propertySymbol?.ToString().Equals("System.Data.SqlClient.SqlCommand.CommandText") ?? false)
                ? assignmentExpression
                : null;
        }

        /// <summary>
        /// Returns true if the specified assignment expression's source is a string literal, or a constant string field.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="assignmentExpression">The assignment expression.</param>
        protected bool IsAssignmentSourceStringLiteralOrConstStringField(
            SyntaxNodeAnalysisContext context,
            AssignmentExpressionSyntax assignmentExpression) {
            var left = assignmentExpression.Left;
            var leftKind = left.Kind();
            var right = assignmentExpression.Right;
            var rightKind = right.Kind();
            if (rightKind == SyntaxKind.AddExpression) {
                var addLeft = ((BinaryExpressionSyntax)right).Left;
                var addLeftKind = addLeft.Kind();
                var addRight = ((BinaryExpressionSyntax)right).Right;
                var addRightKind = addRight.Kind();
                return true;
            }


            return true;
        }

        //private bool RENAMEme(SemanticModel semanticModel, ExpressionSyntax expression) {
        //    if (expression.Kind() == SyntaxKind.AddExpression) {
        //        if (!RENAMEme(semanticModel, ((BinaryExpressionSyntax)expression).Left) ||
        //            !RENAMEme(semanticModel, ((BinaryExpressionSyntax)expression).Right)) {
        //            return false;
        //        }
        //    }

        //    if (expression is LiteralExpressionSyntax) {
        //        return true;
        //    }

        //    if (!(expression is IdentifierNameSyntax)) {
        //        return false;
        //    }

        //    IFieldSymbol fieldSymbol = ModelExtensions.GetSymbolInfo(semanticModel, expression).Symbol as IFieldSymbol;

        //    return (fieldSymbol?.IsConst ?? false) &&
        //           fieldSymbol.Type.ToString().Equals("string") &&
        //           fieldSymbol.Type.ContainingNamespace.ToString().Equals("System");
        //}

        ///// <summary>
        ///// Returns true if the specified assignment expression's source is a string literal, or a constant string field.
        ///// </summary>
        ///// <param name="context">The context.</param>
        ///// <param name="assignmentExpression">The assignment expression.</param>
        //protected bool IsAssignmentSourceStringLiteralOrConstStringField(
        //    SyntaxNodeAnalysisContext context,
        //    AssignmentExpressionSyntax assignmentExpression) {
        //    if (assignmentExpression?.Right is LiteralExpressionSyntax) {
        //        return true;
        //    }

        //    if (!(assignmentExpression?.Right is IdentifierNameSyntax)) {
        //        return false;
        //    }

        //    IFieldSymbol fieldSymbol = context.SemanticModel.GetSymbolInfo(assignmentExpression.Right).Symbol as IFieldSymbol;

        //    return (fieldSymbol?.IsConst ?? false) &&
        //           fieldSymbol.Type.ToString().Equals("string") &&
        //           fieldSymbol.Type.ContainingNamespace.ToString().Equals("System");
        //}
    }
}
