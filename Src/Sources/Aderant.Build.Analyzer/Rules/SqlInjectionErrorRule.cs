using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {
    internal class SqlInjectionErrorRule : SqlInjectionRuleBase {
        internal const string DiagnosticId = "Aderant_SqlInjectionError";

        internal static readonly Tuple<string, string>[] ValidSuppressionMessages = {
            new Tuple<string, string>("\"SQL Injection\"", "\"Aderant_SqlInjectionError\""),
            new Tuple<string, string>("\"Microsoft.Security\"", "\"CA2100:")
        };

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

        private void AnalyzeNodeCommandText(SyntaxNodeAnalysisContext context) {
            var expression = context.Node as ExpressionStatementSyntax;

            if (expression == null ||
                IsAnalysisSuppressed(expression, ValidSuppressionMessages)) {
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

        private void AnalyzeNodeDatabaseSqlQuery(SyntaxNodeAnalysisContext context) {
            var expression = context.Node as InvocationExpressionSyntax;

            if (expression == null ||
                IsAnalysisSuppressed(expression, ValidSuppressionMessages)) {
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

        private void AnalyzeNodeNewSqlCommand(SyntaxNodeAnalysisContext context) {
            var expression = context.Node as ObjectCreationExpressionSyntax;

            if (expression == null ||
                IsAnalysisSuppressed(expression, ValidSuppressionMessages)) {
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
    }
}
