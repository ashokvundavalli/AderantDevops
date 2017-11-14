using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.CodeQuality {
    internal class CodeQualitySystemDiagnosticsRule : RuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_CodeQuality_SystemDiagnostics";

        internal static Tuple<string, string>[] ValidSuppressionMessages = {
            new Tuple<string, string>("\"Code Quality\"", "\"Aderant_SystemDiagnostics\""),
        };

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

        internal override string Title => "System Diagnostics Error";

        internal override string MessageFormat => "Illegal usage of 'System.Diagnostics.Debugger' committed to source. " +
                                                  "Remove usage of '{0}'.";

        internal override string Description => "Remove usage of 'System.Diagnostics.Debugger'.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNodeMethodInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeNodeMethodInvocation(SyntaxNodeAnalysisContext context) {
            var node = (context.Node as InvocationExpressionSyntax)?.Expression as MemberAccessExpressionSyntax;

            if (node == null ||
                IsAnalysisSuppressed(node, ValidSuppressionMessages, true)) {
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

            if (displayString.Equals("System.Diagnostics.Debugger.Launch()", StringComparison.Ordinal) ||
                displayString.Equals("System.Diagnostics.Debugger.Break()", StringComparison.Ordinal)) {
                context.ReportDiagnostic(Diagnostic.Create(Descriptor, node.GetLocation(), displayString));
            }
        }

        #endregion Methods
    }
}
