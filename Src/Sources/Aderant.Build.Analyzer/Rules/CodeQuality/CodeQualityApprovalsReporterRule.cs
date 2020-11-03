using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.CodeQuality {
    internal class CodeQualityApprovalsReporterRule : RuleBase{
        #region Fields

        internal const string DiagnosticId = "Aderant_CodeQuality_ApprovalsReporter";

        private static HashSet<string> whitelistedReporters = new HashSet<string>() {
            "ApprovalTests.Reporters.TfsVnextReporter",
            "ApprovalTests.Reporters.MsTestReporter",
            "ApprovalTests.Reporters.QuietReporter"
        };

        #endregion Fields

        #region Properties

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Error;

        internal override string Id => DiagnosticId;

        internal override string Title => "Approvals Reporter Error";

        internal override string MessageFormat => $"Illegal use of 'UseReporterAttribute' committed to source.{Environment.NewLine}{Description}";

        internal override string Description => $"Remove all attribute arguments aside from the following : {string.Join(", ", whitelistedReporters)}.";

        public override DiagnosticDescriptor Descriptor => new DiagnosticDescriptor(
            Id,
            Title,
            MessageFormat,
            AnalyzerCategory.Syntax,
            Severity,
            true,
            Description);
        
        #endregion Properties

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeAttributeInvocation, SyntaxKind.Attribute);
        }

        private void AnalyzeAttributeInvocation(SyntaxNodeAnalysisContext context) {
            var node = context.Node as AttributeSyntax;

            if(node == null || 
               IsAnalysisSuppressed(node, DiagnosticId, true)) {
                return;
            }

            // See if the attribute constructor used belongs to the UseReporterAttribute class.
            var attributeDisplayString = context.SemanticModel.GetSymbolInfo(node).Symbol?.OriginalDefinition?.ToDisplayString();
            if (string.IsNullOrWhiteSpace(attributeDisplayString) || 
                !attributeDisplayString.StartsWith("ApprovalTests.Reporters.UseReporterAttribute.UseReporterAttribute(")) {
                return;
            }

            // Check the arguments supplied to the attribute constructor.
            if (AttributeContainsInvalidReporter(node, context)) {
                ReportDiagnostic(context, Descriptor, node.GetLocation(), node);
            }
        }

        internal bool AttributeContainsInvalidReporter(AttributeSyntax node, SyntaxNodeAnalysisContext context) {
            var attributeArguments = node.ArgumentList?.ChildNodes()?.ToList();

            if (attributeArguments == null || attributeArguments.Count == 0) {
                return false;
            }

            foreach (var argument in attributeArguments) {
                var attributeArg = argument as AttributeArgumentSyntax;

                if (attributeArg?.Expression is TypeOfExpressionSyntax typeOfExpression) {
                    var argumentType = typeOfExpression.Type as IdentifierNameSyntax;

                    if (argumentType == null) {
                        continue;
                    }

                    var argumentDisplayString = context.SemanticModel.GetSymbolInfo(argumentType).Symbol?.OriginalDefinition?.ToDisplayString();

                    if (string.IsNullOrWhiteSpace(argumentDisplayString) || whitelistedReporters.Contains(argumentDisplayString)) {
                        continue;
                    }

                    return true;
                }
            }
            
            return false;
        }
    }
}
