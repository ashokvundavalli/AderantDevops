using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Aderant.Build.Analyzer.Rules {
    public abstract class RuleBase {
        protected enum RuleViolationSeverityEnum {
            Invalid = -1,
            None,
            Info,
            Warning,
            Error,
            Max
        }

        protected const int DefaultCapacity = 25;

        internal abstract DiagnosticSeverity Severity { get; }

        internal abstract string Id { get; }

        internal abstract string Title { get; }

        internal abstract string MessageFormat { get; }

        internal abstract string Description { get; }

        public abstract DiagnosticDescriptor Descriptor { get; }

        public abstract void Initialize(AnalysisContext context);

        /// <summary>
        /// Recursively iterates through all child nodes,
        /// adding any expressions of the specified type to the referenced list.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="expressionList">The invocation expression list.</param>
        /// <param name="node">The node.</param>
        /// <param name="stopNode">
        /// Acts as a shortcut in the syntax tree recursive search method.
        /// Searching will stop when this node is found in the tree.
        /// </param>
        /// <returns>True if the 'StopNode' has been found, otherwise False.</returns>
        protected static bool GetExpressionsFromChildNodes<T>(
            ref List<T> expressionList,
            SyntaxNode node,
            SyntaxNode stopNode = null) where T : SyntaxNode {
            foreach (var childNode in node.ChildNodes()) {
                // Syntax tree navigation will cease if this node is found.
                if (childNode.Equals(stopNode)) {
                    return true;
                }

                var expression = childNode as T;

                if (expression != null) {
                    expressionList.Add(expression);
                }

                // Recursion.
                if (GetExpressionsFromChildNodes(ref expressionList, childNode, stopNode)) {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Gets the method expression for the parent method of the specified node.
        /// </summary>
        /// <param name="node">The node.</param>
        protected static T GetNodeParentExpressionOfType<T>(SyntaxNode node)
            where T : SyntaxNode {
            return node.Ancestors().OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// Examines the specified context to determine if the method containing
        /// the targeted node includes a code analysis suppression attribute.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="validSuppressionMessages">The valid suppression messages.</param>
        protected static bool IsAnalysisSuppressed(
            SyntaxNode node,
            Tuple<string, string>[] validSuppressionMessages) {
            // Get the parent method of the node.
            var baseMethodDeclaration = GetNodeParentExpressionOfType<BaseMethodDeclarationSyntax>(node);

            // If node is not within a method, exit early.
            if (baseMethodDeclaration == null) {
                return false;
            }

            // Get the parent class of the node.
            var parentClass = GetNodeParentExpressionOfType<ClassDeclarationSyntax>(baseMethodDeclaration);

            // If node is not within a class...
            if (parentClass == null) {
                // Examine suppression on the method only.
                return IsAnalysisSuppressed(baseMethodDeclaration.AttributeLists, validSuppressionMessages);
            }

            // ...otherwise examine suppression on the class, and then the method.
            return IsAnalysisSuppressed(parentClass.AttributeLists, validSuppressionMessages) ||
                   IsAnalysisSuppressed(baseMethodDeclaration.AttributeLists, validSuppressionMessages);
        }

        protected static bool IsAnalysisSuppressed(IEnumerable<AttributeListSyntax> attributeLists,
            Tuple<string, string>[] validSuppressionMessages) {
            // Attributes can be stacked...
            // [Attribute()]
            // [Attribute()]
            // ...and concatenated...
            // [Attribute(), Attribute()]
            // The below double loop handles both cases, including mix & match.
            foreach (AttributeListSyntax attributeList in attributeLists) {
                foreach (AttributeSyntax attribute in attributeList.Attributes) {
                    string name = attribute.Name.ToString();

                    if (name.Equals("TestClass")) {
                        return true;
                    }

                    // If the attribute is not a suppression message, continue.
                    if (!name.Equals("SuppressMessage") &&
                        !name.Equals("System.Diagnostics.CodeAnalysis.SuppressMessage")) {
                        continue;
                    }

                    // If the argument list is empty, or there are no arguments, continue.
                    // This occurs when analysis runs while the code is being written, but before the syntax is properly complete.
                    if (attribute.ArgumentList == null || attribute.ArgumentList.Arguments.Count < 2) {
                        continue;
                    }

                    // Split the arguments of the suppression message for detailed evaluation.
                    string category = attribute.ArgumentList.Arguments[0].ToString();
                    string checkId = attribute.ArgumentList.Arguments[1].ToString();

                    foreach (var validSuppressionMessage in validSuppressionMessages) {
                        if (category.Equals(validSuppressionMessage.Item1, StringComparison.OrdinalIgnoreCase) &&
                            checkId.StartsWith(validSuppressionMessage.Item2, StringComparison.OrdinalIgnoreCase)) {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
