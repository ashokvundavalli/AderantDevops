using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.IDisposable {
    public class IDisposableFactoryRegistrationRule : IDisposableRuleBase {
        #region Properties

        internal override string Title => "Aderant IDisposable Factory Registration Diagnostic";

        internal override string MessageFormat => "Should not factory register as '{0}' since it does not implement 'IDisposable' " +
                                                  "but the concrete class '{1}' does.";

        internal override string Description => "Object can not be disposed once retrieved from factory.";

        private const string FactoryRegistrationTypeName = "Aderant.Framework.Factories.FactoryRegistrationAttribute";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(ProcessNode, SyntaxKind.ClassDeclaration);
        }

        /// <summary>
        /// Processes the local declaration node.
        /// </summary>
        /// <param name="context">The context.</param>
        private void ProcessNode(SyntaxNodeAnalysisContext context) {
            var node = context.Node as ClassDeclarationSyntax;

            // Exit early if execution is not processing a class declaration,
            //      or if analysis is suppressed.
            //      or if the class doesn't implements System.IDisposable.
            if (node == null ||
                IsAnalysisSuppressed(node, DiagnosticId) ||
                !GetIsNodeDisposable(node, context.SemanticModel)) {
                return;
            }

            var factoryRegistrationAttributes = GetFactoryRegistrationAttributes(node, context.SemanticModel);

            // Exit early if there are no factory registration attributes on the class
            if (!factoryRegistrationAttributes.Any()) {
                return;
            }

            foreach (var attribute in factoryRegistrationAttributes) {
                var expression = GetFactoryRegistrationInterface(attribute);
                if (expression == null) {
                    continue;
                }

                if (!GetIsNodeDisposable(expression.Type, context.SemanticModel) &&
                    !GetIsTypeWhiteListed(context.SemanticModel.GetTypeInfo(expression.Type).Type)) {
                    // If registered interface is not disposable, report a diagnostic.
                    ReportDiagnostic(
                        context,
                        Descriptor,
                        attribute.GetLocation(),
                        attribute,
                        expression.Type,
                        node.Identifier.Text);
                }
            }
        }

        private static TypeOfExpressionSyntax GetFactoryRegistrationInterface(AttributeSyntax attribute) {
            // We want to check for explicit assignments to property InterfaceType, if there isn't one then fall back to the first argument.
            var argument = attribute.ArgumentList.Arguments.FirstOrDefault(x => x.NameEquals?.Name.Identifier.Text == "InterfaceType");
            if (argument != null) {
                return argument.Expression as TypeOfExpressionSyntax;
            }

            return attribute.ArgumentList.Arguments.FirstOrDefault().Expression as TypeOfExpressionSyntax;
        }

        private static List<AttributeSyntax> GetFactoryRegistrationAttributes(
            ClassDeclarationSyntax node,
            SemanticModel semanticModel) {
            return node
                .AttributeLists
                .SelectMany(attributeList => attributeList.Attributes)
                .Where(attribute => semanticModel.GetTypeInfo(attribute).Type.ToString() == FactoryRegistrationTypeName)
                .ToList();
        }

        #endregion Methods
    }
}
