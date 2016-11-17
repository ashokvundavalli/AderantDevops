using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {
    public class SqlInjectionWarningRule : SqlInjectionRuleBase {
        internal const string DiagnosticId = "Aderant_SqlInjectionWarning";

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Warning;

        internal override string Id => DiagnosticId;

        internal override string Title => "SQL Injection Warning";

        internal override string MessageFormat => "Database command is potentially vulnerable to SQL injection. Use Stored Procedure DSL.";

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

        /// <summary>
        /// Analyzes the node for DbCommand.CommandText assignment expressions, and raises a warning.
        /// </summary>
        /// <param name="context">The context.</param>
        private void AnalyzeNodeCommandText(SyntaxNodeAnalysisContext context) {
            AssignmentExpressionSyntax assignmentExpression = GetCommandTextAssignmentExpression(context);

            if (assignmentExpression == null) {
                // Do nothing.
                return;
            }

            if (!IsAssignmentSourceStringLiteralOrConstStringField(context, assignmentExpression)) {
                // Raise an error.
                return;
            }

            Diagnostic diagnostic = Diagnostic.Create(Descriptor, assignmentExpression.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}
