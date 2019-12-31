using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.CodeQuality {
    public class CodeQualityMathRoundRule : RuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_CodeQuality_MathRound";

        internal static Tuple<string, string>[] ValidSuppressionMessages = {
            new Tuple<string, string>("\"Code Quality\"", $"\"{DiagnosticId}\""),
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

        internal override string Title => "Math Rounding Error";

        internal override string MessageFormat => "Use 'Aderant.Framework.Extensions.MathRounding.RoundCurrencyAmount' instead of 'Math.Round' or 'Decimal.Round' for currency rounding, and use 'Aderant.Time.Extensions.TimeIncrementRounding' for time rounding.";

        internal override string Description => "The default rounding method for .NET is ToEven but SQL uses AwayFromZero, so we should keep it consistent within Expert and make sure that instead of calling Math.Round or Decimal.Round, call Aderant.Framework.Extensions.MathRounding.RoundCurrencyAmount instead to avoid discrepancies between classic/SQL vs .net code. User Story ID: 208165";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNodeMethodInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeNodeMethodInvocation(SyntaxNodeAnalysisContext context) {

            var node = context.Node as InvocationExpressionSyntax;
            if (node == null) {
                return;
            }

            var exp = node.Expression as MemberAccessExpressionSyntax;

            if (exp == null ||
                IsAnalysisSuppressed(node, ValidSuppressionMessages) ||
                !"System.Math.Round".EndsWith(exp.GetText().ToString(), StringComparison.Ordinal) &&
                !"System.Decimal.Round".EndsWith(exp.GetText().ToString(), StringComparison.Ordinal) &&
                !"decimal.Round".EndsWith(exp.GetText().ToString(), StringComparison.Ordinal)) {

                return;
            }

            ReportDiagnostic(context, Descriptor, node.GetLocation(), node);
        }

        #endregion

    }
}
