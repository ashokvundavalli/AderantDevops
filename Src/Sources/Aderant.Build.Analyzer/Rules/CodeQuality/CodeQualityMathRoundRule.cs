using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        internal override string MessageFormat => "Illegal usage of 'Math.Round'.";

        internal override string Description => "The default rounding method for .NET is ToEven but SQL uses AwayFromZero, so we should keep it consistent within Expert and make sure every time the Math.Round is called, a MidpointRounding Enum is specified, otherwise there will be discrepancies between classic/SQL vs .net code. User Story ID: 208165";

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
                IsArgumentListValid(node) ||
                !"System.Math.Round".EndsWith(exp.GetText().ToString(), StringComparison.Ordinal)) {

                return;
            }

            ReportDiagnostic(context, Descriptor, node.GetLocation(), node);
        }

        private bool IsArgumentListValid(InvocationExpressionSyntax node) {

            var childNodes = node.ArgumentList.ChildNodes().ToList();

            // MidpointRounding argument is always expected, throw error if the number of child nodes is less than 2,
            // or if the last argument isn't a MidpointRounding enum value.

            if (childNodes.Count() < 2) {
                return false;
            }

            return string.Equals("MidpointRounding", childNodes.LastOrDefault()?.GetFirstToken().Text, StringComparison.Ordinal);
        }

        #endregion

    }
}
