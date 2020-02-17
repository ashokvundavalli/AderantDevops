using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.Security {
    public class SecurityBanModifyingDtdProcessingRule : RuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_Security_DTDProcessing";

        private const string propertyName = "DtdProcessing";
        private const string propertyDefinition = "System.Xml.XmlReaderSettings.DtdProcessing";

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

        internal override string Title => "Ban Modifying DTDProcessing Property";

        internal override string MessageFormat => Description;

        internal override string Description => "Illegal modification of 'DtdProcessing' property. Do not modify the value of this property.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeAssignmentNode, SyntaxKind.SimpleAssignmentExpression);
        }

        private void AnalyzeAssignmentNode(SyntaxNodeAnalysisContext context) {
            var node = context.Node as AssignmentExpressionSyntax;

            // Basic sanity check.
            // This rule is intentionally unsuppressable.
            if (node == null ||
                !GetIsDtdProcessingProperty(context.SemanticModel, node)) {
                return;
            }

            ReportDiagnostic(
                context,
                Descriptor,
                node.GetLocation(),
                node);
        }

        private static bool GetIsDtdProcessingProperty(SemanticModel model, AssignmentExpressionSyntax node) {
            var left = UnwrapParenthesizedExpressionDescending(node.Left);

            // Example:
            // item = new XmlReaderSettings {
            //     DtdProcessing = DtdProcessing.TestValue
            // };
            var leftIdentifier = left as IdentifierNameSyntax;
            if (leftIdentifier != null) {
                if (leftIdentifier.Identifier.Text != propertyName) {
                    return false;
                }

                return (model.GetSymbolInfo(leftIdentifier).Symbol as IPropertySymbol)?
                       .OriginalDefinition
                       .ToDisplayString() == propertyDefinition;
            }

            // Example:
            // item.DtdProcessing = DtdProcessing.TestValue;
            var leftProperty = left as MemberAccessExpressionSyntax;
            if (leftProperty == null ||
                leftProperty.Name.Identifier.Text != propertyName) {
                return false;
            }

            return (model.GetSymbolInfo(leftProperty).Symbol as IPropertySymbol)?
                   .OriginalDefinition
                   .ToDisplayString() == propertyDefinition;
        }

        #endregion Methods
    }
}
