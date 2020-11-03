using System.Collections.Generic;
using System.Linq;
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
            //      or if the class declaration is static.
            //      or if the class is whitelisted.
            if (node == null ||
                IsAnalysisSuppressed(node, DiagnosticId) ||
                GetIsNodeDisposable(node, context.SemanticModel) ||
                GetIsDeclarationStatic(node.Modifiers) ||
                GetIsClassNodeWhitelisted(node, context.SemanticModel)) {
                return;
            }

            // Iterate through each of the class declaration's child nodes that are field or property declarations.
            var childNodes = node
                .ChildNodes()
                .Where(syntaxNode =>
                    syntaxNode is FieldDeclarationSyntax ||
                    syntaxNode is PropertyDeclarationSyntax);

            foreach (var childNode in childNodes) {
                // Ignore any nodes that are not disposable.
                if (!GetIsNodeDisposable(childNode, context.SemanticModel)) {
                    continue;
                }

                // Get the name of all fields/properties being declared.
                // It is possible to declare multiple variables in a single statement.
                // Example: string a, b, c;
                List<string> memberNames = null;

                var field = childNode as FieldDeclarationSyntax;
                if (field != null && !GetIsDeclarationStatic(field.Modifiers)) {
                    memberNames = field.Declaration.Variables.Select(variable => variable.Identifier.Text).ToList();
                } else {
                    var property = childNode as PropertyDeclarationSyntax;
                    if (property != null && !GetIsDeclarationStatic(property.Modifiers)) {
                        memberNames = new List<string> { property.Identifier.Text };
                    }
                }

                // If there are no members being declared,
                // or if all members are only assigned from constructor parameters, ignore them.
                if (memberNames == null ||
                    memberNames.Count < 1 ||
                    memberNames.All(memberName => GetIsNodeAssignedFromConstructorParameter(memberName, node))) {
                    continue;
                }

                // Otherwise raise a diagnostic.
                ReportDiagnostic(
                    context,
                    Descriptor,
                    node.Identifier.GetLocation(),
                    node,
                    node.Identifier.Text);

                // Returning here prevents further evaluation of additional fields and properties,
                // as only one diagnostic per class is necessary,
                // rather than one diagnostic per field or property on a class.
                return;
            }
        }

        #endregion Methods
    }
}
