using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.Analyzer.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules.IDisposable {
    internal class IDisposableFieldPropertyRule : IDisposableRuleBase {
        #region Properties

        internal override string Title => "Aderant IDisposable Field Diagnostic";

        internal override string MessageFormat => "Field or property '{0}' implements 'System.IDisposable' and is not disposed.";

        internal override string Description => "Ensure the object is correctly disposed.";

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
            var classNode = context.Node as ClassDeclarationSyntax;

            // Exit early if execution is not processing a field declaration,
            //      or if analysis is suppressed.
            if (classNode == null ||
                IsAnalysisSuppressed(classNode, ValidSuppressionMessages) ||
                GetIsClassNodeWhitelisted(classNode, context.SemanticModel)) {
                return;
            }

            var declarations = new List<DisposableDeclaration>(DefaultCapacity);

            // Evaluate the class' field declarations.
            EvaluateFields(
                ref declarations,
                classNode,
                context.SemanticModel);

            // Evaluate the class' property declarations.
            EvaluateProperties(
                ref declarations,
                classNode,
                context.SemanticModel);

            // If no declarations were found...
            if (!declarations.Any()) {
                // ...exit early.
                return;
            }

            List<SyntaxNode> actionsNonStatic;
            List<SyntaxNode> actionsStatic;

            // Retrieve all action declarations from the current class declaration, split by non-static/static.
            GetClassActionDeclarations(out actionsNonStatic, out actionsStatic, classNode);

            var diagnosticResults = EvaluateDeclarations(
                declarations,
                actionsNonStatic,
                actionsStatic,
                context.SemanticModel);

            // Iterate through and display each diagnostics raised during declaration evaluation.
            foreach (var diagnostic in diagnosticResults) {
                ReportDiagnostic(
                    context,
                    Descriptor,
                    diagnostic.Item3,
                    diagnostic.Item1,
                    diagnostic.Item2);
            }
        }

        /// <summary>
        /// Evaluates the disposal of collections that contain disposable objects.
        /// </summary>
        /// <param name="fieldIsDisposed">if set to <c>true</c> [field is disposed].</param>
        /// <param name="declaration">The declaration.</param>
        /// <param name="actions">The actions.</param>
        private static void EvaluateCollectionDisposal(
            ref bool fieldIsDisposed,
            DisposableDeclaration declaration,
            IEnumerable<SyntaxNode> actions) {
            // Iterate through each action.
            foreach (var action in actions) {
                // Get all invocation expressions from within the action.
                var invocationExpressions = new List<InvocationExpressionSyntax>(DefaultCapacity);

                GetExpressionsFromChildNodes(ref invocationExpressions, action);

                // If the are no invocation expressions...
                if (!invocationExpressions.Any()) {
                    // ...skip this action.
                    continue;
                }

                // Iterate through each invocation expression.
                foreach (var invocationExpression in invocationExpressions) {
                    // Retrieve the IdentifierNameSyntax child expressions from the invocation expression.
                    var identifierExpressions = invocationExpression
                        .Expression
                        .ChildNodes()
                        .OfType<IdentifierNameSyntax>()
                        .ToList();

                    string methodName;
                    string targetName;

                    // Two child expressions are expected:
                    //      [0]: The object being operated upon.
                    //      [1]: The method being invoked.
                    // If there are fewer than two child expressions...
                    if (identifierExpressions.Count < 2) {
                        // ...if there are no child expressions...
                        if (identifierExpressions.Count < 1) {
                            // ...skip this invocation.
                            continue;
                        }

                        // ...otherwise, examine the invocation as a item?.Method() conditional operator.
                        var parentConditional = identifierExpressions[0].GetAncestorOfType<ConditionalAccessExpressionSyntax>();
                        var name = parentConditional?.Expression as IdentifierNameSyntax;

                        targetName = name?.Identifier.Text;
                        methodName = identifierExpressions[0]?.Identifier.Text;
                    } else {
                        targetName = identifierExpressions[0]?.Identifier.Text;
                        methodName = identifierExpressions[1]?.Identifier.Text;
                    }

                    // If the target being operated upon is not the variable currently being evaluated,
                    //      or if the name of the method is somehow invalid...
                    if (!declaration.Name.Equals(targetName, StringComparison.Ordinal) ||
                        string.IsNullOrWhiteSpace(methodName)) {
                        // ...ignore this invocation.
                        continue;
                    }

                    // Switch on the type of the collection.
                    // Note: This allows for flexible manageament of extension method names.
                    switch (declaration.CollectionType) {
                        case DeclarationCollectionType.Collection:
                        case DeclarationCollectionType.Dictionary:
                        case DeclarationCollectionType.List: {
                            // If the method name is valid...
                            if (methodName.Equals("DisposeItems", StringComparison.Ordinal) ||
                                methodName.Equals("RemoveAndDispose", StringComparison.Ordinal) ||
                                methodName.Equals("RemoveAtAndDispose", StringComparison.Ordinal) ||
                                methodName.Equals("RemoveAllAndDispose", StringComparison.Ordinal) ||
                                methodName.Equals("RemoveRangeAndDispose", StringComparison.Ordinal)) {
                                // The field is considered 'disposed'.
                                fieldIsDisposed = true;
                                return;
                            }

                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Evaluates the specified declarations and returns a list of diagnostics and their locations.
        /// </summary>
        /// <param name="declarations">The declarations.</param>
        /// <param name="actionsNonStatic">The actions non static.</param>
        /// <param name="actionsStatic">The actions static.</param>
        /// <param name="semanticModel">The semantic model.</param>
        private static IEnumerable<Tuple<SyntaxNode, string, Location>> EvaluateDeclarations(
            IEnumerable<DisposableDeclaration> declarations,
            List<SyntaxNode> actionsNonStatic,
            List<SyntaxNode> actionsStatic,
            SemanticModel semanticModel) {
            var diagnostics = new List<Tuple<SyntaxNode, string, Location>>();

            // Iterate through each field.
            foreach (var declaration in declarations) {
                // State-tracking flags.
                bool fieldDisposed = false;

                List<SyntaxNode> actions;

                // If the declaration is static, add the static actions to the list of actions to be evaluated.
                if (declaration.IsStatic) {
                    actions = new List<SyntaxNode>(actionsNonStatic.Count + actionsStatic.Count);
                    actions.AddRange(actionsNonStatic);
                    actions.AddRange(actionsStatic);
                } else {
                    actions = actionsNonStatic;
                }

                switch (declaration.CollectionType) {
                    case DeclarationCollectionType.Collection:
                    case DeclarationCollectionType.Dictionary:
                    case DeclarationCollectionType.List: {
                        EvaluateCollectionDisposal(
                            ref fieldDisposed,
                            declaration,
                            actions);

                        break;
                    }
                    case DeclarationCollectionType.None: {
                        EvaluateActions(
                            ref diagnostics,
                            ref fieldDisposed,
                            declaration,
                            actions,
                            semanticModel);

                        break;
                    }
                }

                // If the field is flagged as requiring disposal,
                //      and is not flagged as being disposed...
                if (!fieldDisposed) {
                    // ...add a new diagnostics to the collection.
                    diagnostics.Add(new Tuple<SyntaxNode, string, Location>(declaration.Node, declaration.Name, declaration.Location));
                }
            }

            return diagnostics;
        }

        /// <summary>
        /// Gets the type of the collection field declaration.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="semanticModel">The semantic model.</param>
        private static DeclarationCollectionType GetFieldPropertyDeclarationCollectionType(
            MemberDeclarationSyntax node,
            SemanticModel semanticModel) {
            SyntaxNode typeNode = null;

            var fieldNode = node as BaseFieldDeclarationSyntax;
            if (fieldNode != null) {
                typeNode = fieldNode.Declaration?.ChildNodes()?.FirstOrDefault();
            } else {
                var propertyNode = node as BasePropertyDeclarationSyntax;
                if (propertyNode != null) {
                    typeNode = propertyNode.ChildNodes()?.FirstOrDefault();
                }
            }

            // If somehow the child node could not be found...
            if (typeNode == null) {
                // ...exit.
                return DeclarationCollectionType.None;
            }

            // Get the symbol for the previously located type node.
            string displayString = semanticModel.GetTypeInfo(typeNode).Type?.OriginalDefinition.ToDisplayString();

            // Return the type of the declaration collection.
            if (string.IsNullOrWhiteSpace(displayString)) {
                return DeclarationCollectionType.None;
            }

            if (string.Equals("System.Reactive.Disposables.CompositeDisposable", displayString, StringComparison.Ordinal)) {
                return DeclarationCollectionType.None;
            }

            if (string.Equals("System.Collections.Generic.Dictionary<TKey, TValue>", displayString, StringComparison.Ordinal)) {
                return DeclarationCollectionType.Dictionary;
            }

            if (string.Equals("System.Collections.Generic.List<TKey, TValue>", displayString, StringComparison.Ordinal) ||
                string.Equals("System.Collections.Generic.IList<TKey, TValue>", displayString, StringComparison.Ordinal)) {
                return DeclarationCollectionType.List;
            }

            return displayString.StartsWith("System.Collections.Generic.", StringComparison.Ordinal) ||
                   displayString.StartsWith("System.Collections.ObjectModel.", StringComparison.Ordinal)
                ? DeclarationCollectionType.Collection
                : DeclarationCollectionType.None;
        }

        /// <summary>
        /// Evaluates the specified class' field declarations.
        /// </summary>
        /// <param name="declarations">The declarations.</param>
        /// <param name="classNode">The class node.</param>
        /// <param name="semanticModel">The semantic model.</param>
        private static void EvaluateFields(
            ref List<DisposableDeclaration> declarations,
            ClassDeclarationSyntax classNode,
            SemanticModel semanticModel) {
            var fields = classNode.ChildNodes().OfType<FieldDeclarationSyntax>();

            // Iterate through each field declaration found within the class.
            foreach (var fieldDeclaration in fields) {
                // Ignore any field declarations that do not implement System.IDisposable.
                if (!GetIsNodeDisposable(fieldDeclaration, semanticModel)) {
                    continue;
                }

                // Get the type of collection declaration.
                DeclarationCollectionType declarationCollectionType = GetFieldPropertyDeclarationCollectionType(
                    fieldDeclaration,
                    semanticModel);

                // Determine if the field is static.
                bool isFieldStatic = GetIsDeclarationStatic(fieldDeclaration.Modifiers);

                // A single field declaration may have multiple fields created using declarator syntax.
                // Example:
                //      public static int item0 = 0, item1, item2 = 2;
                // Add each individual declarator-defined field to the collection of fields to evalaute.
                declarations.AddRange(
                    fieldDeclaration
                        .Declaration
                        .Variables
                        // Only evaluate members that are assigned values that are not constructor parameters.
                        .Where(field => !GetIsNodeAssignedFromConstructorParameter(field.Identifier.Text, classNode))
                        .Select(
                            field => new DisposableDeclaration(
                                field,
                                field.Identifier.Text,
                                field.Identifier.GetLocation(),
                                // Declaration is not considered 'assigned' if the assigned value is a literal e.g. 'null'.
                                field.Initializer != null && !(field.Initializer.Value is LiteralExpressionSyntax),
                                isFieldStatic,
                                declarationCollectionType)));
            }
        }

        /// <summary>
        /// Evaluates the specified actions.
        /// </summary>
        /// <param name="diagnostics">The diagnostics.</param>
        /// <param name="fieldIsDisposed">if set to <c>true</c> [field is disposed].</param>
        /// <param name="field">The field.</param>
        /// <param name="actions">The actions.</param>
        /// <param name="semanticModel">The semantic model.</param>
        private static void EvaluateActions(
            ref List<Tuple<SyntaxNode, string, Location>> diagnostics,
            ref bool fieldIsDisposed,
            DisposableDeclaration field,
            IEnumerable<SyntaxNode> actions,
            SemanticModel semanticModel) {
            // Iterate through each action.
            foreach (var action in actions) {
                var expressionNodes = new List<SyntaxNode>(DefaultCapacity * DefaultCapacity);

                // Get every syntax expression within the current action.
                GetExpressionsFromChildNodes(ref expressionNodes, action);

                // Process the previously acquired syntax expressions to create an ordered list of expressions,
                //      within the current action, that modify the specified variable.
                var orderedExpressions = GetOrderedExpressionTypes(
                    expressionNodes,
                    field.Name,
                    semanticModel);

                // If no expressions modify the field...
                if (orderedExpressions?.Any() != true) {
                    // ...skip this action.
                    continue;
                }

                // If the field is assigned at declaration,
                //      and the first operation performed upon the field is some form of assignment...
                if (field.IsAssignedAtDeclaration &&
                    (orderedExpressions[0].Item2 == ExpressionType.Assignment ||
                     orderedExpressions[0].Item2 == ExpressionType.AssignmentNull ||
                     orderedExpressions[0].Item2 == ExpressionType.UsingAssignment)) {
                    // ...add a new diagnostic to the collection.
                    diagnostics.Add(new Tuple<SyntaxNode, string, Location>(field.Node, field.Name, field.Location));

                    // Then skip the action.
                    continue;
                }

                // If the field is not flagged as being disposed,
                //      and the first operation on the field is some form of disposal...
                if (!fieldIsDisposed &&
                    (orderedExpressions[0].Item2 == ExpressionType.Dispose ||
                     orderedExpressions[0].Item2 == ExpressionType.Using)) {
                    // ...update the flag to indicate the field is disposed.
                    fieldIsDisposed = true;
                }

                // Iterate through each ordered expression.
                for (int i = 0; i < orderedExpressions.Count; ++i) {
                    // If the expression is not assigning to the field...
                    if (orderedExpressions[i].Item2 != ExpressionType.Assignment) {
                        // ...ignore it.
                        continue;
                    }

                    // If the current expression is the last expression in the current action that modifies the field...
                    if (i + 1 == orderedExpressions.Count) {
                        // ...cease evaluating the ordered expressions.
                        break;
                    }

                    // If the next expression in order is not some form of disposal expression...
                    if (orderedExpressions[i + 1].Item2 != ExpressionType.Dispose &&
                        orderedExpressions[i + 1].Item2 != ExpressionType.Using) {
                        // ...add a new error to the error collection.
                        diagnostics.Add(new Tuple<SyntaxNode, string, Location>(orderedExpressions[i].Item1, field.Name, orderedExpressions[i].Item3));
                    }
                }
            }
        }

        /// <summary>
        /// Evaluates the specified property declarations.
        /// </summary>
        /// <param name="declarations">The declarations.</param>
        /// <param name="classNode">The class node.</param>
        /// <param name="semanticModel">The semantic model.</param>
        private static void EvaluateProperties(
            ref List<DisposableDeclaration> declarations,
            ClassDeclarationSyntax classNode,
            SemanticModel semanticModel) {
            var properties = classNode.ChildNodes().OfType<PropertyDeclarationSyntax>();

            // Iterate through each property declaration found within the class.
            foreach (var propertyDeclaration in properties) {
                // Ignore any property declarations that do not implement System.IDisposable.
                // Ignore any properties that are simply wrappers for backing fields.
                // Ignore any properties that are only assigned values from constructors.
                if (!GetIsNodeDisposable(propertyDeclaration, semanticModel) ||
                    GetIsPropertyFieldWrapper(propertyDeclaration, declarations) ||
                    GetIsNodeAssignedFromConstructorParameter(propertyDeclaration.Identifier.Text, classNode)) {
                    continue;
                }

                // Get the type of collection declaration.
                DeclarationCollectionType declarationCollectionType = GetFieldPropertyDeclarationCollectionType(
                    propertyDeclaration,
                    semanticModel);

                // Determine if the property is static.
                bool isPropertyStatic = GetIsDeclarationStatic(propertyDeclaration.Modifiers);

                // Add the property to the list of declarations that must be disposed.
                declarations.Add(
                    new DisposableDeclaration(
                        propertyDeclaration,
                        propertyDeclaration.Identifier.Text,
                        propertyDeclaration.Identifier.GetLocation(),
                        propertyDeclaration.Initializer != null &&
                        !(propertyDeclaration.Initializer.Value is LiteralExpressionSyntax),
                        isPropertyStatic,
                        declarationCollectionType));
            }
        }

        /// <summary>
        /// Retrieves all method and property accessor declarations from the specified parent class, split by non-static/static.
        /// </summary>
        /// <param name="actionsNonStatic">The non static actions.</param>
        /// <param name="actionsStatic">The static actions.</param>
        /// <param name="classDeclaration">The class declaration.</param>
        private static void GetClassActionDeclarations(
            out List<SyntaxNode> actionsNonStatic,
            out List<SyntaxNode> actionsStatic,
            ClassDeclarationSyntax classDeclaration) {
            actionsNonStatic = new List<SyntaxNode>(DefaultCapacity);
            actionsStatic = new List<SyntaxNode>(DefaultCapacity);

            foreach (var declaration in classDeclaration.ChildNodes().OfType<BaseMethodDeclarationSyntax>()) {
                if (GetIsDeclarationStatic(declaration.Modifiers)) {
                    actionsStatic.Add(declaration);
                } else {
                    actionsNonStatic.Add(declaration);
                }
            }

            foreach (var declaration in classDeclaration.ChildNodes().OfType<PropertyDeclarationSyntax>()) {
                if (GetIsDeclarationStatic(declaration.Modifiers)) {
                    if (declaration.AccessorList != null) {
                        actionsStatic.AddRange(declaration.AccessorList.Accessors);
                    } else {
                        actionsStatic.Add(declaration.ExpressionBody);
                    }
                } else {
                    if (declaration.AccessorList != null) {
                        actionsNonStatic.AddRange(declaration.AccessorList.Accessors);
                    } else {
                        actionsNonStatic.Add(declaration.ExpressionBody);
                    }
                }
            }
        }

        /// <summary>
        /// Determines whether the specified property is a wrapper for backing a field
        /// already contained within the specified list of declarations.
        /// </summary>
        private static bool GetIsPropertyFieldWrapper(
            PropertyDeclarationSyntax propertyDeclaration,
            IReadOnlyList<DisposableDeclaration> declarations) {
            // Evaluate the property for an 'ArrowExpression'.
            // Example:
            //      public DisposeMe Item => backingField;
            var arrowExpression = propertyDeclaration
                .ChildNodes()
                .OfType<ArrowExpressionClauseSyntax>()
                .FirstOrDefault();

            // If an arrow expression is found...
            if (arrowExpression != null) {
                var identifiers = new List<IdentifierNameSyntax>();

                // ...get all the identifier expressions contained within the arrow expression.
                GetExpressionsFromChildNodes(ref identifiers, arrowExpression);

                // If any of the identifier expressions returned is a reference
                //      to a field already contained within the declarations collection,
                //      then the property is a wrapper for a backing field and can be ignored.
                if (declarations.Any(
                    declaration =>
                        identifiers.Any(
                            identifierExpression =>
                                identifierExpression
                                    .Identifier
                                    .Text
                                    .Equals(declaration.Name, StringComparison.Ordinal)))) {
                    return true;
                }
            }

            // Find the first 'GetExpression' from the first list of 'AccessorSyntax'.
            // Example:
            //      public DisposeMe Item {
            //          get { return backingField; }  <-- GetExpression
            //          set { backingField = value; }
            //      }
            var getExpression = propertyDeclaration
                .ChildNodes()
                .OfType<AccessorListSyntax>()
                .FirstOrDefault()?
                .ChildNodes()
                .OfType<AccessorDeclarationSyntax>()
                .FirstOrDefault(x => x.Kind() == SyntaxKind.GetAccessorDeclaration);

            // If a get expression is not found...
            if (getExpression == null) {
                // ...exit early.
                return false;
            }

            var returnExpressions = new List<ReturnStatementSyntax>();

            // ...get all the return statement expressions contained within the get expression.
            GetExpressionsFromChildNodes(ref returnExpressions, getExpression);

            // If any of the identifier expressions returned is a reference
            //      to a field already contained within the declarations collection,
            //      then the property is a wrapper for a backing field and can be ignored.
            foreach (var returnExpression in returnExpressions) {
                var identifierExpressions = new List<IdentifierNameSyntax>();
                GetExpressionsFromChildNodes(ref identifierExpressions, returnExpression);

                // Return if any of the declarations' variables match any of the variables returned from the get expression.
                return declarations.Any(
                    declaration => identifierExpressions.Any(
                        expression => expression
                            .Identifier
                            .Text
                            .Equals(
                                declaration.Name,
                                StringComparison.Ordinal)));
            }

            // Only occurs if there are no 'return expressions'.
            // (uncompilable code, get expression is currently being refactored)
            return false;
        }

        #endregion Methods
    }
}
