using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.CodeQuality {
    internal class CodeQualityDbConnectionStringRule : RuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_CodeQuality_DbConnectionString";

        #endregion Fields

        #region Properties

        public override DiagnosticDescriptor Descriptor => new DiagnosticDescriptor(
            Id,
            Title,
            MessageFormat,
            AnalyzerCategory.Syntax,
            Severity,
            true,
            Description);

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Error;

        internal override string Id => DiagnosticId;

        internal override string Title => "DbConnectionString Error";

        internal override string MessageFormat => Description;

        internal override string Description => "Remove illegal usage of DbConnectionString. Use Installation.Current.ConnectionString.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNodeMethodInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeNodeMethodInvocation(SyntaxNodeAnalysisContext context) {
            var node = context.Node as InvocationExpressionSyntax;

            if (node == null ||
                IsAnalysisSuppressed(node, DiagnosticId)) {
                return;
            }

            string displayString = context
                .SemanticModel
                .GetSymbolInfo(node)
                .Symbol?
                .OriginalDefinition?
                .ToDisplayString();

            if (string.IsNullOrWhiteSpace(displayString)) {
                return;
            }

            if (!displayString.Equals(
                "Aderant.Framework.Persistence.FrameworkDb.CreateConnection()",
                StringComparison.Ordinal)) {
                return;
            }

            var parentExpression = node.Parent as MemberAccessExpressionSyntax;

            if (parentExpression == null) {
                return;
            }

            if (parentExpression.Name.ToString().Equals("ConnectionString", StringComparison.Ordinal)) {
                ReportDiagnostic(context, Descriptor, node.GetLocation(), node);
            }
        }

        #endregion Methods
    }
}
