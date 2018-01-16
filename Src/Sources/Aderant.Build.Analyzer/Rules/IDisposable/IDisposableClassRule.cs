using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.IDisposable {
    internal class IDisposableClassRule : IDisposableRuleBase {
        #region Properties

        internal override string Title => "Aderant IDisposable Class Diagnostic";

        internal override string MessageFormat => "Class '{0}' must implement the 'System.IDisposable' interface " +
                                                  "as it contains a field or property that inherits from 'System.IDisposable'.";

        internal override string Description => "Ensure the object is correctly disposed.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(ProcessNode, SyntaxKind.ClassDeclaration);
        }

        /// <summary>
        /// Processes the class declaration node.
        /// </summary>
        /// <param name="context">The context.</param>
        private void ProcessNode(SyntaxNodeAnalysisContext context) {
            var node = context.Node as ClassDeclarationSyntax;

            // Exit early if execution is not processing a class declaration,
            //      or if analysis is suppressed.
            //      or if the class already implements System.IDisposable.
            if (node == null ||
                IsAnalysisSuppressed(node, ValidSuppressionMessages) ||
                GetIsNodeDisposable(node, context.SemanticModel) ||
                GetIsDeclarationStatic(node.Modifiers) ||
                GetIsClassNodeWhitelisted(node, context.SemanticModel)) {
                return;
            }

            // Iterate through each of the class declaration's child nodes.
            foreach (var childNode in node.ChildNodes()) {
                // If the child node is a field or property declaration,
                //      and the child node implements System.IDisposable...
                if ((childNode is FieldDeclarationSyntax || childNode is PropertyDeclarationSyntax) &&
                    GetIsNodeDisposable(childNode, context.SemanticModel)) {
                    // ...report a diagnostic.
                    ReportDiagnostic(
                        context,
                        Descriptor,
                        node.Identifier.GetLocation(),
                        node,
                        node.Identifier.Text);
                    return;
                }
            }
        }

        #endregion Methods
    }
}
