using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {
    internal class SqlInjectionWarningRule : SqlInjectionRuleBase {
        internal const string DiagnosticId = "Aderant_SqlInjectionWarning";

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Warning;

        internal override string Id => DiagnosticId;

        internal override string Title => "SQL Injection Warning";

        internal override string MessageFormat => "Database command is potentially vulnerable to SQL injection. Consider using Stored Procedure DSL.";

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
            if (IsProjectIgnored(context) ||
                EvaluateNodeCommandTextExpressionStatement(
                    context.SemanticModel,
                    (ExpressionStatementSyntax)context.Node) != SqlInjectionRuleViolationSeverityEnum.Warning) {
                return;
            }

            Diagnostic diagnostic = Diagnostic.Create(Descriptor, context.Node.GetLocation());
            context.ReportDiagnostic(diagnostic);
        }
    }
}
