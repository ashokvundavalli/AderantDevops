using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.IDisposable {
    internal class IDisposableConstructorRule : IDisposableRuleBase {
        #region Types

        private class ConstructorData {
            public ConstructorData(
                ConstructorDeclarationSyntax constructor,
                bool isProtectedOrPublic,
                IReadOnlyList<INamedTypeSymbol> signature,
                IReadOnlyList<INamedTypeSymbol> initializerSignature) {
                Constructor = constructor;
                IsProtectedOrPublic = isProtectedOrPublic;
                Signature = signature;
                InitializerSignature = initializerSignature;
            }

            public ConstructorDeclarationSyntax Constructor { get; }
            public bool IsProtectedOrPublic { get; }
            public IReadOnlyList<INamedTypeSymbol> Signature { get; }
            public IReadOnlyList<INamedTypeSymbol> InitializerSignature { get; }

            public bool HasDisposableSignature() {
                return Signature?.Any(GetIsDisposable) == true;
            }

            public bool HasDisposableInitializerSignature() {
                return InitializerSignature?.Any(GetIsDisposable) == true;
            }
        }

        #endregion Types

        #region Properties

        internal override string Title => "Aderant IDisposable Invocation Diagnostic";

        internal override string MessageFormat => "Parameter '{0}' implements 'System.IDisposable' and must be disposed.";

        internal override string Description =>
            "'System.IDisposable' parameters to non-public constructors must be properly disposed, " +
            "unless the parameter is provided via a public constructor and is external to the class.";

        #endregion Properties

        #region Methods

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(ProcessNode, SyntaxKind.ClassDeclaration);
        }

        /// <summary>
        /// Processes the node.
        /// </summary>
        /// <param name="context">The context.</param>
        private void ProcessNode(SyntaxNodeAnalysisContext context) {
            var classNode = context.Node as ClassDeclarationSyntax;

            // Exit early if execution is not processing a class declaration,
            //      or if analysis is suppressed.
            if (classNode == null ||
                IsAnalysisSuppressed(classNode, ValidSuppressionMessages)) {
                return;
            }

            // Retrieve all of the class' constructors.
            var constructors = new List<ConstructorDeclarationSyntax>();
            GetExpressionsFromChildNodes(ref constructors, classNode);

            // Exit early if there are no constructors.
            if (constructors.Count < 1) {
                return;
            }

            var constructorData = GetConstructorData(constructors, context.SemanticModel);

            EvaluateConstructorsInternalPrivate(context, Descriptor, constructorData);
            EvaluateConstructorsProtectedPublic(context, Descriptor, constructorData);
        }

        /// <summary>
        /// Evaluates the internal and private constructors.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="descriptor">The descriptor.</param>
        /// <param name="constructorData">The constructor data.</param>
        private static void EvaluateConstructorsInternalPrivate(
            SyntaxNodeAnalysisContext context,
            DiagnosticDescriptor descriptor,
            IReadOnlyCollection<ConstructorData> constructorData) {
            // Iterate through constructor data.
            foreach (var dataIntPriv in constructorData) {
                // If the current constructor is not internal or private,
                // or none of the types in its signature are disposable...
                if (dataIntPriv.IsProtectedOrPublic ||
                    !dataIntPriv.HasDisposableSignature()) {
                    // ...ignore it.
                    continue;
                }

                // If any of the constructors has an initializer with a signature that matches the signature of this constructor...
                // Note:
                //      This implicitely checks accessability, as code will not compile if two constructors share
                //      the same signature, and any internal or private constructor that has another internal or private
                //      constructor as its initializer, will be evaluated seperately.
                if (constructorData.Any(data => data.InitializerSignature.SequenceEqual(dataIntPriv.Signature))) {
                    // ...this consturctor is valid and can be ignored.
                    continue;
                }

                // Retrieve the list of parameters to the constructor.
                var parameters = dataIntPriv.Constructor.ParameterList.Parameters;

                // Iterate through each of the parameters.
                for (int i = 0; i < parameters.Count; ++i) {
                    // If the parameter is of type 'System.IDisposable'...
                    // ...and the parameter is NOT 'utilized' within the constructor (e.g. assigned or disposed)...
                    // Note:
                    //      Use the constructor's signature data to determine parameter type,
                    //      as the data is already available, decreasing the processing required.
                    if (GetIsDisposable(dataIntPriv.Signature[i]) &&
                        !GetIsParameterUtilized(parameters[i], dataIntPriv.Constructor)) {
                        // ...report a diagnostic.
                        ReportDiagnostic(
                            context,
                            descriptor,
                            parameters[i].GetLocation(),
                            parameters[i],
                            parameters[i].Identifier.Text);
                    }
                }
            }
        }

        /// <summary>
        /// Evaluates the protected and public constructors.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="descriptor">The descriptor.</param>
        /// <param name="constructorData">The constructor data.</param>
        private static void EvaluateConstructorsProtectedPublic(
            SyntaxNodeAnalysisContext context,
            DiagnosticDescriptor descriptor,
            IReadOnlyCollection<ConstructorData> constructorData) {
            // Iterate through constructor data.
            foreach (var dataProtPub in constructorData) {
                // If the current constructor is not protected or public,
                // or none of the types in its initializer's signature are disposable...
                if (!dataProtPub.IsProtectedOrPublic ||
                    !dataProtPub.HasDisposableInitializerSignature()) {
                    continue;
                }

                // Retrieve the constructor's parameters, and its initializer's arguments.
                var parameters = dataProtPub.Constructor.ParameterList.Parameters;
                var arguments = dataProtPub.Constructor.Initializer.ArgumentList.Arguments;

                // A list containing the indexes for types within the constructor's initializer,
                // that are not also parameters to the constructor.
                var indexes = new List<int>();

                // Iterate through each of the arguments.
                for (int i = 0; i < arguments.Count; ++i) {
                    // Note:
                    //      Use the constructor's initializer's signature data to determine parameter type,
                    //      as the data is already available, decreasing the processing required.
                    if (!GetIsDisposable(dataProtPub.InitializerSignature[i])) {
                        continue;
                    }

                    // Retrieve the argument as an identifier name.
                    var argument = arguments[i].Expression as IdentifierNameSyntax;

                    // If the argument is an identifier, then it is a variable of some description.
                    // If the variable also exists within the constructor's parameters...
                    if (argument != null &&
                        parameters.Any(parameter => string.Equals(argument.Identifier.Text, parameter.Identifier.Text, StringComparison.Ordinal))) {
                        // ...then the 'System.IDisposable' value was passed in from a source external to the current class,
                        // and is therefore the caller's responsability to properly dispose.
                        continue;
                    }

                    // Otherwise, record the index at which the unknown object was located within the initializer.
                    indexes.Add(i);
                }

                // Locate the private or internal constructor with a signature that matches the signature of the current constructor's inintializer.
                // Note:
                //      As above, this check implicitely includes assignemnt due to code compilation requirements.
                ConstructorData linkedConstructorData = constructorData
                    .FirstOrDefault(data => data.Signature.SequenceEqual(dataProtPub.InitializerSignature));

                // If no linked constructor was found...
                if (linkedConstructorData == null) {
                    // ...then the constructor does not exist and may be under construction. Code will not compile.
                    continue;
                }

                // Iterate through each of the indexes representing an object of 'unknown' origin.
                foreach (var index in indexes) {
                    // Retrieve the parameters of the linked constructor.
                    var parameter = linkedConstructorData.Constructor.ParameterList.Parameters[index];

                    // If the parameter is NOT utilized by the linked constructor...
                    if (!GetIsParameterUtilized(parameter, linkedConstructorData.Constructor)) {
                        // ...report a diagnostic.
                        ReportDiagnostic(
                            context,
                            descriptor,
                            parameter.GetLocation(),
                            parameter,
                            parameter.Identifier.Text);
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves specific data from the specified constructor declarations and organizes it into a parsable format.
        /// </summary>
        /// <param name="constructors">The constructors.</param>
        /// <param name="semanticModel">The semantic model.</param>
        private static IReadOnlyCollection<ConstructorData> GetConstructorData(
            IEnumerable<ConstructorDeclarationSyntax> constructors,
            SemanticModel semanticModel) {
            var constructorData = new List<ConstructorData>();

            // Iterate through each constructor.
            foreach (var constructor in constructors) {
                // Determine the constructor's accessability.
                var isProtectedOrPublic = GetNodeHasAnyModifiers(constructor, new[] { "protected", "public" });

                // Create the constructor's signature by retrieving the type symbols of each parameter.
                var signature = constructor
                    .ParameterList
                    .Parameters
                    .Select(
                        parameter => semanticModel
                            .GetSymbolInfo(parameter.Type)
                            .Symbol as INamedTypeSymbol)
                    .ToList();

                if (isProtectedOrPublic) {
                    List<INamedTypeSymbol> initializerSignature;

                    // If the constructor has an initializer...
                    if (constructor.Initializer != null) {
                        // Create the initializer's signature by retrieving the type symbols of each argument's expression.
                        initializerSignature = constructor
                            .Initializer
                            .ArgumentList
                            .Arguments
                            .Select(argument => semanticModel.GetTypeInfo(argument.Expression).ConvertedType as INamedTypeSymbol)
                            .ToList();
                    } else {
                        // ...otherwise, use a default initializer.
                        initializerSignature = new List<INamedTypeSymbol>(0);
                    }

                    // Create new constructor data, using the provided initializer.
                    constructorData.Add(new ConstructorData(constructor, true, signature, initializerSignature));
                } else {
                    // Create new constructor data, with a default initializer.
                    constructorData.Add(new ConstructorData(constructor, false, signature, new List<INamedTypeSymbol>(0)));
                }
            }

            return constructorData;
        }

        /// <summary>
        /// Determines whether the specified node is utilized by the specified parent.
        /// </summary>
        /// <param name="param">The parameter.</param>
        /// <param name="parent">The parent.</param>
        private static bool GetIsParameterUtilized(ParameterSyntax param, SyntaxNode parent) {
            var assignmentExpressions = new List<AssignmentExpressionSyntax>();
            GetExpressionsFromChildNodes(ref assignmentExpressions, parent);

            foreach (var assignment in assignmentExpressions) {
                var identifier = assignment.Right as IdentifierNameSyntax;

                if (identifier != null) {
                    if (string.Equals(param.Identifier.Text, identifier.Identifier.Text, StringComparison.Ordinal)) {
                        return true;
                    }

                    continue;
                }

                var identifiers = new List<IdentifierNameSyntax>();
                GetExpressionsFromChildNodes(ref identifiers, assignment.Right);

                if (identifiers.Any(id => string.Equals(param.Identifier.Text, id.Identifier.Text, StringComparison.Ordinal))) {
                    return true;
                }
            }

            var equalsExpressions = new List<EqualsValueClauseSyntax>();
            GetExpressionsFromChildNodes(ref equalsExpressions, parent);

            foreach (var equalsValueClause in equalsExpressions) {
                var identifier = equalsValueClause.Value as IdentifierNameSyntax;

                if (identifier != null) {
                    if (string.Equals(param.Identifier.Text, identifier.Identifier.Text, StringComparison.Ordinal)) {
                        return true;
                    }

                    continue;
                }

                var identifiers = new List<IdentifierNameSyntax>();
                GetExpressionsFromChildNodes(ref identifiers, equalsValueClause.Value);

                if (identifiers.Any(id => string.Equals(param.Identifier.Text, id.Identifier.Text, StringComparison.Ordinal))) {
                    return true;
                }
            }

            var invocationExpressions = new List<InvocationExpressionSyntax>();
            GetExpressionsFromChildNodes(ref invocationExpressions, parent);

            foreach (var expression in invocationExpressions) {
                var expressionString = expression.ToString();

                if (expressionString.EndsWith(".Dispose()", StringComparison.Ordinal) ||
                    expressionString.EndsWith(".Close()", StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the specified declaration node has any of the specified modifiers.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="modifiers">The modifiers.</param>
        private static bool GetNodeHasAnyModifiers(BaseMethodDeclarationSyntax node, IEnumerable<string> modifiers) {
            return modifiers
                .Any(
                    modifier => node
                        .Modifiers
                        .Select(mod => mod.Text)
                        .Any(mod => string.Equals(modifier, mod, StringComparison.OrdinalIgnoreCase)));
        }

        #endregion
    }
}
