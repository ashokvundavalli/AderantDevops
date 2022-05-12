using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.CodeQuality {
    public class CodeQualitySqlQueryRule : RuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_CodeQuality_SqlQuery";

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

        internal override string Title => "SqlQuery Error";

        internal override string MessageFormat => "Replace the usage of this method with an alternative data access strategy " +
                                                  "such as a Stored Procedure DSL call.";

        internal override string Description => "Customizations made to Entity Framework prevent the usage of this method. " +
                                                "Replace the usage of this method with an alternative data access strategy " +
                                                "such as a Stored Procedure DSL call.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNodeMethodInvocation, SyntaxKind.InvocationExpression);
        }

        private void AnalyzeNodeMethodInvocation(SyntaxNodeAnalysisContext context) {
            var node = (context.Node as InvocationExpressionSyntax)?.Expression as MemberAccessExpressionSyntax;

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
                    "System.Data.Entity.Database.SqlQuery(System.Type, string, params object[])",
                    StringComparison.Ordinal) &&
                !displayString.Equals(
                    "System.Data.Entity.Database.SqlQuery<TElement>(string, params object[])",
                    StringComparison.Ordinal)) {
                return;
            }

            var parentClass = node.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault();

            if (parentClass == null) {
                return;
            }

            var classSymbol = context.SemanticModel.GetDeclaredSymbol(parentClass);

            if (classSymbol == null) {
                return;
            }

            var parentClassName = classSymbol.OriginalDefinition.ToDisplayString();

            const string expertDbContextString = "Aderant.Query.Services.ExpertDbContext";

            if (string.Equals(
                expertDbContextString,
                parentClassName,
                StringComparison.Ordinal)) {
                ReportDiagnostic(context, Descriptor, node.GetLocation(), node);
                return;
            }

            var baseClassName = classSymbol.BaseType.OriginalDefinition.ToDisplayString();

            if (string.Equals(
                expertDbContextString,
                baseClassName,
                StringComparison.Ordinal)) {
                ReportDiagnostic(context, Descriptor, node.GetLocation(), node);
            }
        }

        #endregion Methods
    }
}
