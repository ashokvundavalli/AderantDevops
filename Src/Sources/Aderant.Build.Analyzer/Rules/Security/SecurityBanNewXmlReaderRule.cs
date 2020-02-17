using System.Linq;
using Aderant.Build.Analyzer.Lists.Security;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.Security {
    public class SecurityBanNewXmlReaderRule : RuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_Security_NewXmlReader";

        private const string startText = "System.Xml.XmlTextReader.XmlTextReader(";

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

        internal override string Title => "Ban New XmlReader";

        internal override string MessageFormat => Description;

        internal override string Description => "Illegal creation of 'new XmlTextReader'. Use 'XmlReader.Create()' instead.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeObjectCreationNode, SyntaxKind.ObjectCreationExpression);
        }

        private void AnalyzeObjectCreationNode(SyntaxNodeAnalysisContext context) {
            var node = context.Node as ObjectCreationExpressionSyntax;

            // Basic sanity check.
            // This rule is intentionally unsuppressable.
            if (node == null ||
                context
                    .SemanticModel
                    .GetSymbolInfo(node)
                    .Symbol?
                    .OriginalDefinition?
                    .ToDisplayString()?
                    .StartsWith(startText) != true ||
                GetIsClassWhitelisted(context.SemanticModel, node)) {
                return;
            }

            ReportDiagnostic(
                context,
                Descriptor,
                node.GetLocation(),
                node);
        }

        /// <summary>
        /// Determines if the current class is whitelisted.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="node">The node.</param>
        private static bool GetIsClassWhitelisted(SemanticModel model, SyntaxNode node) {
            return node
                .Ancestors()
                .OfType<ClassDeclarationSyntax>()
                .Select(declaration => model
                    .GetDeclaredSymbol(declaration)
                    .OriginalDefinition
                    .ToDisplayString())
                .Any(definition => SecurityWhitelist.Types.Contains(definition));
        }

        #endregion Methods
    }
}
