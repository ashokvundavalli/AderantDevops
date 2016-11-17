using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {
    public class SqlInjectionErrorRule : SqlInjectionRuleBase {
        internal const string DiagnosticId = "Aderant_SqlInjectionError";

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Error;

        internal override string Id => DiagnosticId;

        internal override string Title => "SQL Injection Error";

        internal override string MessageFormat => "Database command is vulnerable to SQL injection. Use Stored Procedure DSL.";

        internal override string Description => "Use Stored Procedure DSL.";

        public override DiagnosticDescriptor Descriptor => new DiagnosticDescriptor(
            id: Id,
            title: Title,
            messageFormat: MessageFormat,
            category: AnalyzerCategory.Syntax,
            defaultSeverity: Severity,
            isEnabledByDefault: true,
            description: Description);

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNodeCommandText, SyntaxKind.ExpressionStatement);
        }

        private void AnalyzeNodeCommandText(SyntaxNodeAnalysisContext context) {
            AssignmentExpressionSyntax assignmentExpression = GetCommandTextAssignmentExpression(context);

            if (assignmentExpression == null) {
                // Do nothing.
                return;
            }

            if (IsAssignmentSourceStringLiteralOrConstStringField(context, assignmentExpression)) {
                // Raise a warning.
                return;
            }

            Diagnostic diagnostic = Diagnostic.Create(Descriptor, assignmentExpression.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}