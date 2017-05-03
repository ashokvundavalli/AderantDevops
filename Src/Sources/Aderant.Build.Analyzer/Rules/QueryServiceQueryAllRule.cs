using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {
    internal class QueryServiceQueryAllRule : RuleBase {
        internal const string DiagnosticId = "Aderant_QueryAllError";

        internal static Tuple<string, string>[] ValidSuppressionMessages = {
            new Tuple<string, string>("\"SQL Query\"", "\"Aderant_QueryAllError")
        };

        internal override DiagnosticSeverity Severity => DiagnosticSeverity.Error;

        internal override string Id => DiagnosticId;

        internal override string Title => "Query All Error";

        internal override string MessageFormat => "Invocation of Query Service method or property retrieves all results. " +
                                                  "Consider applying filter methods such as 'Where()' and/or 'Select()' before enumerating query results.";

        internal override string Description => string.Empty;

        public override DiagnosticDescriptor Descriptor => new DiagnosticDescriptor(
            Id,
            Title,
            MessageFormat,
            AnalyzerCategory.Syntax,
            Severity,
            true,
            Description);

        public override void Initialize(AnalysisContext context) {
            context.RegisterSyntaxNodeAction(AnalyzeNodePropertyAccessor, SyntaxKind.SimpleMemberAccessExpression);
        }

        private void AnalyzeNodePropertyAccessor(SyntaxNodeAnalysisContext context) {
            // If node is not a member access expression, exit early.
            if (!(context.Node is MemberAccessExpressionSyntax) ||
                IsAnalysisSuppressed(context.Node, ValidSuppressionMessages)) {
                return;
            }

            // Determine if the query is valid.
            if (GetQueryExpressionSeverity(context.SemanticModel, context.Node) == RuleViolationSeverityEnum.None) {
                return;
            }

            // Raise diagnostic.
            context.ReportDiagnostic(Diagnostic.Create(Descriptor, context.Node.GetLocation()));
        }

        /// <summary>
        /// Determines whether the specified query syntax is valid.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="node">The node.</param>
        private static RuleViolationSeverityEnum GetQueryExpressionSeverity(SemanticModel semanticModel, SyntaxNode node) {
            // Retrieve the node's child nodes as a list for easier operation.
            var childNodes = node.ChildNodes().ToList();

            // For a query, a minimum of two children are expected.
            if (childNodes.Count < 2) {
                // Exit early.
                return RuleViolationSeverityEnum.None;
            }

            // Get the fully qualified string value of the type relating to the provided syntax expression.
            // Such as the type of a method's return value, if the syntax is a method.
            // Or the type being constructed, if it's a constructor.
            // Or the type of the variable, if it's a variable.
            string typeString = GetTypeString(semanticModel, childNodes[0]);

            // Empty type is not a Query, exit early.
            if (string.IsNullOrWhiteSpace(typeString)) {
                return RuleViolationSeverityEnum.None;
            }

            // If type is not a Query, exit early.
            if (!typeString.Equals("Aderant.Query.QueryServiceProxy") &&
                !typeString.Equals("Aderant.Query.IQueryServiceProxy") &&
                !typeString.Equals("Aderant.Query.Services.QueryServiceDirect") &&
                !typeString.Equals("Aderant.Query.Services.IQueryServiceDirect") &&
                !typeString.Equals("Aderant.Framework.Query.ConfigurationQuery") &&
                !typeString.Equals("Aderant.Framework.Query.IConfigurationQuery") &&
                !typeString.Equals("Aderant.Query.QueryServiceManagementProxy") &&
                !typeString.Equals("Aderant.Query.IQueryServiceManagementProxy")) {
                return RuleViolationSeverityEnum.None;
            }

            // Get the type of the second child node (the property being accessed).
            // Example: System.Linq.IQueryable<Aderant.Query.ViewModels.Office>
            var genericType = semanticModel.GetTypeInfo(childNodes[1]).Type as INamedTypeSymbol;

            // If the property being accessed does not return a query,
            // or does not contain a valid generic type argument, exit early.
            if (genericType == null ||
                !genericType.IsGenericType ||
                !genericType.ToDisplayString().StartsWith("System.Linq.IQueryable<") ||
                genericType.TypeArguments.Length < 1) {
                return RuleViolationSeverityEnum.None;
            }

            // Get the generic type parameter.
            //                        v-- Generic Type Parameter ---v
            // System.Linq.IQueryable<Aderant.Query.ViewModels.Office>
            var genericTypeParameter = genericType.TypeArguments[0];

            // Get attributes of parameter type.
            var genericTypeParameterAttributes = genericTypeParameter.GetAttributes();

            // Query is valid if queried class is adorned with the 'IsCacheableAttribute' attribute.
            return genericTypeParameterAttributes.Any(
                x => x.AttributeClass.ToDisplayString().Equals("Aderant.Query.Annotations.IsCacheableAttribute"))
                ? RuleViolationSeverityEnum.None
                // Evaluate the methods being invoked upon the query.
                : EvaluateQueryMethods(semanticModel, node, genericTypeParameter);
        }

        /// <summary>
        /// Gets the fully qualified string value of the type associated with the specified node.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="node">The node.</param>
        private static string GetTypeString(SemanticModel semanticModel, SyntaxNode node) {
            // If the specified node is a method invocation or an object creation expression (also a type of method).
            if (node is InvocationExpressionSyntax || node is ObjectCreationExpressionSyntax) {
                // Retrieve the method's symbol.
                var methodSymbol = semanticModel.GetSymbolInfo(node).Symbol as IMethodSymbol;

                // If no symbol was found...
                if (methodSymbol == null) {
                    // ...return default.
                    return string.Empty;
                }

                // If the method is a constructor...
                return methodSymbol.MethodKind == MethodKind.Constructor
                    // ...return the constructed type...
                    ? methodSymbol.ReceiverType.ToDisplayString()
                    // ...otherwise return the method's return type.
                    : methodSymbol.ReturnType.ToDisplayString();
            }

            // If the specified node is a variable of some description...
            if (node is IdentifierNameSyntax) {
                // ...return the variable's type.
                return semanticModel.GetTypeInfo(node).Type?.ToDisplayString();
            }

            // Otherwise, return default.
            return string.Empty;
        }

        /// <summary>
        /// Locates the query's parent node, then evaluates the parent node for methods invoked upon the query.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="node">The node.</param>
        /// <param name="genericTypeParameter"></param>
        private static RuleViolationSeverityEnum EvaluateQueryMethods(
            SemanticModel semanticModel,
            SyntaxNode node,
            ISymbol genericTypeParameter) {
            SyntaxNode parentNode = null;

            // Each possible use case uses a different expression syntax.
            // Instead of individually handling each use case, traverse 
            // the tree of ancestor nodes and retrieve the last node before the block node.
            // This node is only required for its children,
            // and does not require any type-specific handling.
            foreach (var ancestorNode in node.Ancestors()) {
                if (ancestorNode is BlockSyntax) {
                    break;
                }

                parentNode = ancestorNode;
            }

            // If the return expression is null,
            // a query is being invoked, but the results aren't being used.
            return parentNode == null
                ? RuleViolationSeverityEnum.None
                // Evaluate the parent node.
                : EvaluateParentNode(semanticModel, parentNode, node, genericTypeParameter);
        }

        /// <summary>
        /// Evaluates the parent node for methods invoked upon the query.
        /// </summary>
        /// <param name="semanticModel">The semantic model.</param>
        /// <param name="parentNode">The parent node.</param>
        /// <param name="currentNode">The current node.</param>
        /// <param name="genericTypeParameter"></param>
        private static RuleViolationSeverityEnum EvaluateParentNode(
            SemanticModel semanticModel,
            SyntaxNode parentNode,
            SyntaxNode currentNode,
            ISymbol genericTypeParameter) {
            // Get all method invocations applied to the query.
            var methods = new List<InvocationExpressionSyntax>(DefaultCapacity);
            GetExpressionsFromChildNodes(ref methods, parentNode, currentNode);

            // If no methods were found...
            if (!methods.Any()) {
                // If the parent node is a foreach statement, the query is automatically enumerated.
                // Otherwise the query is not enumerated, and is therefore still valid.
                return parentNode is ForEachStatementSyntax
                    ? RuleViolationSeverityEnum.Error
                    : RuleViolationSeverityEnum.None;
            }

            // Note:
            // The 'parentNode' may include methods as children, that are not relevant to the query being executed.
            // This is a side effect of having a 'generic' method retrieval process that handles multiple use-cases
            // where a query may be invoked. The process below will ignore methods that are not relevant to the query.
            IMethodSymbol methodSymbol = null;

            // Results are returned in order from right to left.
            // Example: GetObject().ToString();
            // Above example would return:
            // [0]: ToString()
            // [1]: GetObject()
            // Using a descending 'for' loop is more performant than reversing the existing list.
            for (int i = methods.Count - 1; i >= 0; --i) {
                // If the target node is a paremeter to the current method, ignore the method.
                if (IsNodeParameterOfMethod(methods[i], currentNode)) {
                    continue;
                }

                methodSymbol = semanticModel.GetSymbolInfo(methods[i]).Symbol as IMethodSymbol;

                // If the method symbol was not retrieved...
                if (methodSymbol == null) {
                    // ...default to valid.
                    return RuleViolationSeverityEnum.None;
                }

                // If the method is an 'Expand Clause'...
                if (methodSymbol.Name.Equals("Expand") &&
                    methodSymbol.ContainingType.ToDisplayString().Equals("Aderant.Framework.Extensions.QueryableExtensions")) {
                    // ...continue and ignore it.
                    methodSymbol = null;
                    continue;
                }

                // Break if the method is not an 'Expand Clause'.
                break;
            }

            if (methodSymbol == null ||
                // If the method returns an IQueryable, the query is filtered and therefore valid.
                methodSymbol.ReturnType.Name.Equals("IQueryable") ||
                methodSymbol.ReturnType.ToDisplayString().Equals(genericTypeParameter.ToDisplayString())) {
                return RuleViolationSeverityEnum.None;
            }

            // Determine if the returned type inherits from IQueryable.
            return methodSymbol.ReturnType.AllInterfaces.Any(x => x.Name.Equals("IQueryable"))
                ? RuleViolationSeverityEnum.None
                : RuleViolationSeverityEnum.Error;
        }

        /// <summary>
        /// Determines whether the specified node is a parameter to the specified method.
        /// </summary>
        /// <param name="method">The method.</param>
        /// <param name="node">The node.</param>
        private static bool IsNodeParameterOfMethod(InvocationExpressionSyntax method, SyntaxNode node) {
            if (method.ArgumentList == null || !method.ArgumentList.Arguments.Any()) {
                return false;
            }

            foreach (var argument in method.ArgumentList.Arguments) {
                List<SyntaxNode> allChildNodes = new List<SyntaxNode>(DefaultCapacity);
                if (!argument.Expression.Equals(node) && !GetExpressionsFromChildNodes(ref allChildNodes, argument.Expression, node)) {
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
