using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {
    internal class SqlInjectionErrorRule : SqlInjectionRuleBase {
        internal const string DiagnosticId = "Aderant_SqlInjectionError";

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Error;

        internal override string Id => DiagnosticId;

        internal override string Title => "SQL Injection Error";

        internal override string MessageFormat => "Database command is vulnerable to SQL injection. Use Stored Procedure DSL.";

        internal override string Description => "Use Stored Procedure DSL.";

        public override DiagnosticDescriptor Descriptor => new DiagnosticDescriptor(
            Id,
            Title,
            MessageFormat,
            AnalyzerCategory.Syntax,
            Severity,
            true,
            Description);

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNodeCommandText, SyntaxKind.ExpressionStatement);
            context.RegisterSyntaxNodeAction(AnalyzeNodeDatabaseSqlQuery, SyntaxKind.InvocationExpression);
            context.RegisterSyntaxNodeAction(AnalyzeNodeNewSqlCommand, SyntaxKind.ObjectCreationExpression);
        }

        private void AnalyzeNodeCommandText(SyntaxNodeAnalysisContext context) {
            if (IsProjectIgnored(context) ||
                IsAnalysisSuppressed(context) ||
                EvaluateNodeCommandTextExpressionStatement(
                    context.SemanticModel,
                    (ExpressionStatementSyntax)context.Node) != SqlInjectionRuleViolationSeverityEnum.Error) {
                return;
            }

            Diagnostic diagnostic = Diagnostic.Create(Descriptor, context.Node.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        private void AnalyzeNodeDatabaseSqlQuery(SyntaxNodeAnalysisContext context) {
            if (IsProjectIgnored(context) ||
                IsAnalysisSuppressed(context) ||
                EvaluateNodeDatabaseSqlQuery(
                    context.SemanticModel,
                    (InvocationExpressionSyntax)context.Node) != SqlInjectionRuleViolationSeverityEnum.Error) {
                return;
            }

            Diagnostic diagnostic = Diagnostic.Create(Descriptor, context.Node.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }

        private void AnalyzeNodeNewSqlCommand(SyntaxNodeAnalysisContext context) {
            if (IsProjectIgnored(context) ||
                IsAnalysisSuppressed(context) ||
                EvaluateNodeNewSqlCommandObjectCreationExpression(
                    context.SemanticModel,
                    (ObjectCreationExpressionSyntax)context.Node) != SqlInjectionRuleViolationSeverityEnum.Error) {
                return;
            }

            Diagnostic diagnostic = Diagnostic.Create(Descriptor, context.Node.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}
