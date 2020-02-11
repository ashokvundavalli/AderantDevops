using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.CodeQuality {
    public class CodeQualityDataProviderIllegalPublicPropertiesRule : RuleBase {
        #region Fields

        internal const string DiagnosticId = "Aderant_CodeQuality_DataProviderIllegalPublicProperties";

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

        internal override string Title => "Data Provider Illegal Public Property";

        internal override string MessageFormat => Description;

        internal override string Description => "All public properties that contain public setters " +
                                                "must be assigned to local variables " +
                                                "before being used within a method on a DataProvider.";

        #endregion Properties

        #region Methods

        /// <summary>
        /// Initializes the specified context.
        /// </summary>
        /// <param name="context">The context.</param>
        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNodeClassDeclaration, SyntaxKind.ClassDeclaration);
        }

        /// <summary>
        /// Analyzes the node class declaration.
        /// </summary>
        /// <param name="context">The context.</param>
        private void AnalyzeNodeClassDeclaration(SyntaxNodeAnalysisContext context) {
            var node = context.Node as ClassDeclarationSyntax;

            if (node == null ||
                // The below ensures that this rule
                // cannot be formally suppressed within the source code.
                // Though suppression via the GlobalSuppression.cs file,
                // and thus the automated suppression, is still honoured.
                IsAnalysisSuppressed(node, new Tuple<string, string>[0])) {
                return;
            }

            // Confirm the class declaration being examined is a dataprovider.
            if (!GetIsDataProvider(context.SemanticModel, node)) {
                return;
            }

            // Get a collection containing all public properties with public setters,
            // from the current class declaration.
            var properties = node
                .ChildNodes()
                .OfType<PropertyDeclarationSyntax>()
                .Where(GetIsPropertyPublicWithPublicSetter)
                .ToList();

            // If there are no public properties with public setters, exit early.
            if (properties.Count < 1) {
                return;
            }

            // Get a collection of all method declarations from the current class declaration.
            var methods = node
                .ChildNodes()
                .OfType<MethodDeclarationSyntax>();

            // For each method method declaration,
            // find every illegal usage of each property, and raise a diagnostic.
            foreach (var propertyUsage in GetIllegalPropertyUsages(methods, properties)) {
                ReportDiagnostic(context, Descriptor, propertyUsage.GetLocation(), propertyUsage);
            }
        }

        /// <summary>
        /// Compares the contents of each <see cref="MethodDeclarationSyntax"/>
        /// against the specified <see cref="PropertyDeclarationSyntax"/>es and
        /// returns the <see cref="SimpleNameSyntax"/> usage of any property
        /// used illegally within the source code.
        /// </summary>
        /// <param name="methods">The methods.</param>
        /// <param name="properties">The properties.</param>
        private static IEnumerable<SimpleNameSyntax> GetIllegalPropertyUsages(
            IEnumerable<MethodDeclarationSyntax> methods,
            IEnumerable<PropertyDeclarationSyntax> properties) {
            var illegalPropertyUsages = new List<SimpleNameSyntax>(DefaultCapacity);

            foreach (var method in methods) {
                var identifierSyntaxes = new List<IdentifierNameSyntax>(DefaultCapacity * DefaultCapacity);
                GetExpressionsFromChildNodes(ref identifierSyntaxes, method);

                if (identifierSyntaxes.Count < 1) {
                    continue;
                }

                illegalPropertyUsages
                    .AddRange(identifierSyntaxes
                        .Where(identifier =>
                            GetIsPropertyUsageIllegal(identifier, properties)));
            }

            return illegalPropertyUsages;
        }

        /// <summary>
        /// Determines if the specified <see cref="SimpleNameSyntax"/> identifier is used illegally within the source code.
        /// </summary>
        /// <param name="identifier">The identifier.</param>
        /// <param name="properties">The properties.</param>
        private static bool GetIsPropertyUsageIllegal(SimpleNameSyntax identifier, IEnumerable<PropertyDeclarationSyntax> properties) {
            // Only match the currently examined identifier against the specified properties,
            // as this method will be called against every identifier found in every declared method in the source code.
            if (properties.All(prop => prop.Identifier.Text != identifier.Identifier.Text)) {
                return false;
            }

            var parent = UnwrapParenthesizedExpressionAscending(identifier.Parent);

            // Examine the syntax tree above the current node.
            // Below is the only tree structure that is considered valid,
            // as this is the tree structure of a simple local variable declaration and assignment.
            if (parent is EqualsValueClauseSyntax &&
                parent.Parent is VariableDeclaratorSyntax &&
                parent.Parent.Parent is VariableDeclarationSyntax &&
                parent.Parent.Parent.Parent is LocalDeclarationStatementSyntax) {
                return false;
            }

            // Property usage is illegal.
            // Each usage is individually tracked so diagnostics
            // can be raised at specific locations within the source code.
            return true;
        }

        /// <summary>
        /// Determines if the specified <see cref="ClassDeclarationSyntax"/> is a DataProvider,
        /// by examining the declaration's attributes for the 'DataProviderPropertyAttribute'.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="node">The node.</param>
        private static bool GetIsDataProvider(SemanticModel model, ClassDeclarationSyntax node) {
            foreach (var attribute in GetAttributesFromDeclaration(node)) {
                var symbol = model.GetSymbolInfo(attribute).Symbol;

                if (symbol?.OriginalDefinition.ToDisplayString() ==
                    "Aderant.PresentationFramework.Windows.Data." +
                    "DataProviderRegistrationAttribute." +
                    "DataProviderRegistrationAttribute(System.Type)") {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines if the specified <see cref="BasePropertyDeclarationSyntax"/> is public,
        /// and that it has a 'set' <see cref="AccessorDeclarationSyntax"/> that is also public.
        /// </summary>
        /// <param name="property">The property.</param>
        private static bool GetIsPropertyPublicWithPublicSetter(BasePropertyDeclarationSyntax property) {
            // Ignore any properties that are not public.
            if (property
                .ChildTokens()
                .All(token => token.Kind() != SyntaxKind.PublicKeyword)) {
                return false;
            }

            var setter = property
                .AccessorList
                .Accessors
                .FirstOrDefault(accessor => accessor.Kind() == SyntaxKind.SetAccessorDeclaration);

            // Ignore any properties that have no public setter.
            return setter != null &&
                   GetIsAccessorPublic(setter);
        }

        /// <summary>
        /// Determines if the specified <see cref="AccessorDeclarationSyntax"/> is public.
        /// </summary>
        /// <param name="accessor">The accessor.</param>
        private static bool GetIsAccessorPublic(AccessorDeclarationSyntax accessor) {
            foreach (var token in accessor.ChildTokens()) {
                switch (token.Kind()) {
                    case SyntaxKind.PrivateKeyword:
                    case SyntaxKind.InternalKeyword:
                    case SyntaxKind.ProtectedKeyword: {
                        return false;
                    }
                }
            }

            return true;
        }

        #endregion Methods
    }
}
