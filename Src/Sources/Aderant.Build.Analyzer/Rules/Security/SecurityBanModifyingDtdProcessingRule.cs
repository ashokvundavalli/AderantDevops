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

        internal override string Description => "Illegal modification of 'DtdProcessing' property. " +
                                                "Only direct assignments of 'System.Xml.DtdProcessing.Prohibit' and 'System.Xml.DtdProcessing.Ignore' values are valid.";

        #endregion Properties

        #region Methods

        /// <summary>
        /// Initializes the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeAssignmentNode, SyntaxKind.SimpleAssignmentExpression);
        }

        /// <summary>
        /// Analyzes the assignment node.
        /// </summary>
        /// <param name="context">The context.</param>
        private void AnalyzeAssignmentNode(SyntaxNodeAnalysisContext context) {
            var node = context.Node as AssignmentExpressionSyntax;

            // Basic sanity check.
            // This rule is intentionally unsuppressable.
            if (node == null ||
                ValidateAssignment(context.SemanticModel, node)) {
                return;
            }

            ReportDiagnostic(
                context,
                Descriptor,
                node.GetLocation(),
                node);
        }

        /// <summary>
        /// Validates the <see cref="AssignmentExpressionSyntax"/>.
        /// If the assignment is not to a DtdProcessing property,
        /// or the property value is valid, return true, else false.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="node">The node.</param>
        private static bool ValidateAssignment(
            SemanticModel model,
            AssignmentExpressionSyntax node) {
            var left = UnwrapParenthesizedExpressionDescending(node.Left);

            if (!GetIsDtdProcessingProperty(model, left)) {
                // Assignment is not a DTDProcessing property, and is therefore valid.
                return true;
            }

            var right = UnwrapParenthesizedExpressionDescending(node.Right) as MemberAccessExpressionSyntax;
            if (right == null) {
                // Assignment is a DTDProcessing property, but a non-enum value is assigned
                // or an indirect DtdProcessing enum value is assigned to it.
                // This is invalid.
                return false;
            }

            var rightTypeInfo = model.GetTypeInfo(right.Expression);
            if (rightTypeInfo.ConvertedType.ToDisplayString() != "System.Xml.DtdProcessing") {
                // Assignment value is not a 'DtdProcessing' enum.
                // This should not compile, but this sanity check exists for safety.
                return false;
            }

            // The only valid assignment values are 'DtdProcessing.Prohibit' and 'DtdProcessing.Ignore'.
            return right.Name.Identifier.Text == "Prohibit" ||
                   right.Name.Identifier.Text == "Ignore";
        }

        /// <summary>
        /// Determines if the provided <see cref="SyntaxNode"/> is a DTDProcessing property.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="node">The node.</param>
        private static bool GetIsDtdProcessingProperty(
            SemanticModel model,
            SyntaxNode node) {
            // Example:
            // item = new XmlReaderSettings {
            //     DtdProcessing = DtdProcessing.TestValue
            // };
            var leftIdentifier = node as IdentifierNameSyntax;
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
            var leftProperty = node as MemberAccessExpressionSyntax;
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
