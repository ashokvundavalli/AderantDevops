using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Aderant.Build.Analyzer.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using NodeData = Aderant.Build.Analyzer.Extensions.RoslynExtensions.NodeData;

namespace Aderant.Build.Analyzer.Rules {
    public abstract class RuleBase {
        #region Type Definitions

        protected enum RuleViolationSeverityEnum {
            Invalid = -1,
            None,
            Info,
            Warning,
            Error,
            Max
        }

        #endregion Type Definitions

        #region Fields

        protected const int DefaultCapacity = 25;

        private const string generatedCodeTypeName = "System.CodeDom.Compiler.GeneratedCodeAttribute";
        private const string suppressMessageTypeName = "System.Diagnostics.CodeAnalysis.SuppressMessage";
        private const string testClassTypeName = "Microsoft.VisualStudio.TestTools.UnitTesting.TestClass";

        private const string startString = "[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage(\"Aderant.GeneratedSuppression\", \"";
        private const string targetString = "\", Target = \"";
        private const string endString = "\")]";

        #endregion Fields

        #region Properties

        internal abstract DiagnosticSeverity Severity { get; }

        internal abstract string Id { get; }

        internal abstract string Title { get; }

        internal abstract string MessageFormat { get; }

        internal abstract string Description { get; }

        public abstract DiagnosticDescriptor Descriptor { get; }

        #endregion Properties

        #region Methods

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
        /// <returns>
        /// True if the 'StopNode' has been found, otherwise False.
        /// </returns>
        protected static bool GetExpressionsFromChildNodes<T>(
            ref List<T> expressionList,
            SyntaxNode node,
            SyntaxNode stopNode = null) where T : SyntaxNode {
            foreach (var childNode in node.ChildNodes()) {

                // Stop nodes can exist inside assignment expressions
                // checking both sides of the expression to make sure it doesn't exist within and ceasing navigation if it does.
                var assignmentExpression = childNode as AssignmentExpressionSyntax;
                if (assignmentExpression != null) {
                    if (assignmentExpression.Left.Equals(stopNode) || assignmentExpression.Right.Contains(stopNode)) {
                        return true;
                    }
                }

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
        /// Examines the specified context to determine if the method containing
        /// the targeted node includes a code analysis suppression attribute.
        /// Does not allow analysis of Unit Test classes.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="validSuppressionMessages">The valid suppression messages.</param>
        protected static bool IsAnalysisSuppressed(
            SyntaxNode node,
            Tuple<string, string>[] validSuppressionMessages) {
            return IsAnalysisSuppressed(node, validSuppressionMessages, false);
        }

        /// <summary>
        /// Examines the specified context to determine if the method containing
        /// the targeted node includes a code analysis suppression attribute.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="validSuppressionMessages">The valid suppression messages.</param>
        /// <param name="analyzeTests">if set to <c>true</c> [analyze tests].</param>
        protected static bool IsAnalysisSuppressed(
            SyntaxNode node,
            Tuple<string, string>[] validSuppressionMessages,
            bool analyzeTests) {
            var classVariable = node as ClassDeclarationSyntax;

            if (classVariable != null) {
                return IsAnalysisSuppressed(classVariable.AttributeLists, validSuppressionMessages, analyzeTests);
            }

            var attributeLists = node.GetAncestorOfType<BaseMethodDeclarationSyntax>()?.AttributeLists;

            if (attributeLists == null) {
                attributeLists = node.GetAncestorOfType<AccessorDeclarationSyntax>()?.AttributeLists;

                if (attributeLists == null) {
                    // Node is not within a method, exit early.
                    return false;
                }
            }

            // Analyze method data, returning true if analysis is suppressed at the method level.
            if (IsAnalysisSuppressed(attributeLists.Value, validSuppressionMessages, analyzeTests)) {
                return true;
            }

            // Get the class of the node.
            classVariable = node.GetAncestorOfType<ClassDeclarationSyntax>();

            // If node is not within a class...
            if (classVariable == null) {
                // Examine suppression on the method only.
                return IsAnalysisSuppressed(attributeLists.Value, validSuppressionMessages, analyzeTests);
            }

            // ...otherwise examine suppression on the class, and then the method.
            return IsAnalysisSuppressed(classVariable.AttributeLists, validSuppressionMessages, analyzeTests) ||
                   IsAnalysisSuppressed(attributeLists.Value, validSuppressionMessages, analyzeTests);
        }

        /// <summary>
        /// Examines the specified context to determine if the method containing
        /// the targeted node includes a code analysis suppression attribute.
        /// </summary>
        /// <param name="attributeLists">The attribute lists.</param>
        /// <param name="validSuppressionMessages">The valid suppression messages.</param>
        /// <param name="analyzeTests">if set to <c>true</c> [analyze tests].</param>
        protected static bool IsAnalysisSuppressed(
            IEnumerable<AttributeListSyntax> attributeLists,
            Tuple<string, string>[] validSuppressionMessages,
            bool analyzeTests) {
            // Attributes can be stacked...
            // [Attribute()]
            // [Attribute()]
            // ...and concatenated...
            // [Attribute(), Attribute()]
            // The below double loop handles both cases, including mix & match.
            foreach (AttributeListSyntax attributeList in attributeLists) {
                foreach (AttributeSyntax attribute in attributeList.Attributes) {
                    string name = attribute.Name.ToString();

                    // If the class is a test class, automatically suppress any diagnostics.
                    if (!analyzeTests && testClassTypeName.EndsWith(name, StringComparison.Ordinal)) {
                        return true;
                    }

                    // If the class is generated code, suppress any diagnostics.
                    if (generatedCodeTypeName.EndsWith(name, StringComparison.Ordinal)) {
                        return true;
                    }

                    // If the attribute is not a suppression message, continue.
                    if (!suppressMessageTypeName.EndsWith(name, StringComparison.Ordinal)) {
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

        #endregion Methods

        #region Methods: Auto Suppression

        /// <summary>
        /// Reports the diagnostic.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="descriptor">The descriptor.</param>
        /// <param name="location">The location.</param>
        /// <param name="messageArgs">The message arguments.</param>
        /// Note:
        ///     Automatic suppression for SymbolAnalysisContext diagnostics is not currently supported.
        protected static void ReportDiagnostic(
            SymbolAnalysisContext context,
            DiagnosticDescriptor descriptor,
            Location location,
            params object[] messageArgs) {
            context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArgs));
        }

        /// <summary>
        /// Reports the diagnostic.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="descriptor">The descriptor.</param>
        /// <param name="location">The location.</param>
        /// <param name="node">The node.</param>
        /// <param name="messageArgs">The message arguments.</param>
        protected static void ReportDiagnostic(
            SyntaxNodeAnalysisContext context,
            DiagnosticDescriptor descriptor,
            Location location,
            SyntaxNode node,
            params object[] messageArgs) {
            if (ProcessAutoSuppression(node.GetNodeData(), descriptor.Id, context.SemanticModel)) {
                context.ReportDiagnostic(Diagnostic.Create(descriptor, location, messageArgs));
            }
        }

        /// <summary>
        /// Processes automatic suppression.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="diagnosticId">The diagnostic identifier.</param>
        /// <param name="semanticModel">The semantic model.</param>
        /// <returns>
        /// A value indicating whether a diagnostic should be raised.
        /// </returns>
        private static bool ProcessAutoSuppression(
            NodeData data,
            string diagnosticId,
            SemanticModel semanticModel) {
            string[] contents;

            // Attempt to retrieve suppressions file path and contents.
            bool? tryResult = TryGetGlobalSuppressionsFileContents(data, out contents);

            // Suppressions file contents could not be read.
            if (tryResult == false) {
                // Raise diagnostic.
                return true;
            }

#if !AUTOSUPPRESS
            // Suppressions file does not exist.
            if (tryResult == null) {
                // Raise diagnostic.
                return true;
            }

            // Generate suppression message.
            string message = GenerateSuppressionMessage(data.Node, diagnosticId, semanticModel);

            // Raise a diagnostic if the contents of the suppressions file
            // does not include the generated suppression message.
            return !contents.Contains(message);
#else // AUTOSUPPRESS
            // Generate suppression message.
            string message = GenerateSuppressionMessage(data.Node, diagnosticId, semanticModel);

            // If the suppressions file does not exist...
            if (tryResult == null) {
                // ...create it and suppress this node.
                SetFileContents(data.SuppressionFilePath, new[] { message }, Encoding.Unicode);

                // Add the suppressions file to the project file.
                AddSuppressionsFileToProject(data);

                // Do not raise a diagnostic.
                return false;
            }

            // If the suppression message is already contained within the suppressions file...
            if (contents.Contains(message)) {
                // ...do not raise a diagnostic.
                return false;
            }

            // Create a new file contents container, one element larger than the existing contents container.
            var newContents = new string[contents.Length + 1];

            // Iterate through the existing contents container, copying each element into the new container.
            for (int i = 0; i < contents.Length; ++i) {
                newContents[i] = contents[i];
            }

            // Set the single additional element of the new contents container, to be the new suppression message.
            newContents[contents.Length] = message;

            // Write the new contents to the GlobalSuppressions.cs file.
            SetFileContents(data.SuppressionFilePath, newContents, Encoding.Unicode);

            // Do not raise a diagnostic.
            return false;
#endif
        }

        /// <summary>
        /// Tries the get the current <seealso cref="Project" />'s GlobalSuppressions.cs file's contents.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="contents">The contents.</param>
        /// <returns>
        /// true  - File exists, and contents were retrieved without error.
        /// false - File does not exist (standard), or an errored occurred while retrieving contents.
        /// null  - File does not exist (auto-suppression).
        /// </returns>
        private static bool? TryGetGlobalSuppressionsFileContents(
            NodeData data,
            out string[] contents) {
            contents = null;
            if (!File.Exists(data.SuppressionFilePath)) {
#if !AUTOSUPPRESS
                return false;
#else // AUTOSUPPRESS
                return null;
#endif
            }

            try {
                contents = File.ReadAllLines(data.SuppressionFilePath);
            } catch (Exception) {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Builds the suppression message.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="diagnosticId">The diagnostic identifier.</param>
        /// <param name="content">The content.</param>
        private static string BuildSuppressionMessage(
            SyntaxNode node,
            string diagnosticId,
            string content) {
            // Namespace.
            string namespaceString = GenerateSuppressionMessageNamespace(node);

            // Build the message.
            var stringBuilder = new StringBuilder(string.Concat(startString, diagnosticId, targetString));

            stringBuilder.Append(string.Concat(namespaceString, ":"));
            stringBuilder.Append(content);
            stringBuilder.Append(endString);

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Generates the suppression message.
        /// Contains handling for various 'node' types.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="diagnosticId">The diagnostic identifier.</param>
        /// <param name="semanticModel">The semantic model.</param>
        private static string GenerateSuppressionMessage(
            SyntaxNode node,
            string diagnosticId,
            SemanticModel semanticModel) {
            if (node is ObjectCreationExpressionSyntax ||
                node is InvocationExpressionSyntax) {
                return GenerateSuppressionMessageCreationInvocation(node, diagnosticId, semanticModel);
            }

            if (node is ClassDeclarationSyntax ||
                node is VariableDeclaratorSyntax ||
                node is PropertyDeclarationSyntax ||
                node is ParameterSyntax) {
                return GenerateSuppressionMessageIdentifier(node, diagnosticId);
            }

            var attributeSyntax = node as AttributeSyntax;
            return attributeSyntax != null
                ? GenerateSuppressionMessageAttribute(attributeSyntax, diagnosticId)
                : GenerateSuppressionMessageExpression(node, diagnosticId);
        }

        /// <summary>
        /// Generates the suppression message from an identifier.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="diagnosticId">The diagnostic identifier.</param>
        private static string GenerateSuppressionMessageIdentifier(
            SyntaxNode node,
            string diagnosticId) {
            // Handling for objects that lack a common base class with an Indentifier.
            var property = node as PropertyDeclarationSyntax;
            if (property != null) {
                return BuildSuppressionMessage(node, diagnosticId, property.Identifier.Text);
            }

            var classObject = node as ClassDeclarationSyntax;
            if (classObject != null) {
                return BuildSuppressionMessage(node, diagnosticId, classObject.Identifier.Text);
            }

            var variableDeclarator = node as VariableDeclaratorSyntax;
            if (variableDeclarator != null) {
                return BuildSuppressionMessage(node, diagnosticId, variableDeclarator.Identifier.Text);
            }

            var parameter = node as ParameterSyntax;
            return parameter != null
                ? BuildSuppressionMessage(node, diagnosticId, parameter.Identifier.Text)
                : null;
        }

        /// <summary>
        /// Generates the expression suppression message.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="diagnosticId">The diagnostic identifier.</param>
        private static string GenerateSuppressionMessageExpression(
            SyntaxNode node,
            string diagnosticId) {
            // Name.
            string name = null;

            if (node is AssignmentExpressionSyntax) {
                name = "AssignmentExpression";
            } else if (node is EqualsValueClauseSyntax) {
                name = "EqualsValueClause";
            } else if (node is ExpressionSyntax){
                name = "Expression";
            }

            return name == null
                ? null
                : BuildSuppressionMessage(node, diagnosticId, name);
        }

        /// <summary>
        /// Generates the attribute suppression message.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="diagnosticId">The diagnostic identifier.</param>
        private static string GenerateSuppressionMessageAttribute(
            AttributeSyntax node,
            string diagnosticId) {
            return BuildSuppressionMessage(
                node,
                diagnosticId,
                string.Concat(node.Name.ToString(), "Attribute"));
        }

        /// <summary>
        /// Generates the creation/invocation suppression message.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <param name="diagnosticId">The diagnostic identifier.</param>
        /// <param name="semanticModel">The semantic model.</param>
        private static string GenerateSuppressionMessageCreationInvocation(
            SyntaxNode node,
            string diagnosticId,
            SemanticModel semanticModel) {
            string name = semanticModel.GetSymbolInfo(node).Symbol?.OriginalDefinition.Name;

            // Class.
            string classString = GenerateSuppressionMessageClassDeclaration(node);

            // Parent.
            string parent = GenerateSuppressionMessageBaseMethodDeclaration(node) ??
                            GenerateSuppressionMessagePropertyDeclaration(node);

            // Namespace.
            string namespaceString = GenerateSuppressionMessageNamespace(node);

            // Build the message.
            var stringBuilder = new StringBuilder(string.Concat(startString, diagnosticId, targetString));

            stringBuilder.Append(string.Concat(namespaceString, ":"));
            stringBuilder.Append(string.Concat(classString, ":"));

            if (parent != null) {
                stringBuilder.Append(string.Concat(parent, ":"));
            }

            stringBuilder.Append(name);
            stringBuilder.Append(endString);

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Generates the base method declaration suppression message.
        /// </summary>
        /// <param name="node">The node.</param>
        private static string GenerateSuppressionMessageBaseMethodDeclaration(
            SyntaxNode node) {
            var baseMethod = node.GetAncestorOfType<BaseMethodDeclarationSyntax>();

            if (baseMethod == null) {
                return null;
            }

            string name;

            var constructor = baseMethod as ConstructorDeclarationSyntax;

            if (constructor != null) {
                name = constructor.Identifier.Text;
            } else {
                var method = baseMethod as MethodDeclarationSyntax;
                name = method?.Identifier.Text;
            }

            int count = baseMethod.ParameterList.Parameters.Count;
            var parameters = new string[count];

            // Get the parameter type stings.
            for (int i = 0; i < count; ++i) {
                parameters[i] = baseMethod.ParameterList.Parameters[i].Type.ToString();
            }

            var parmsString = string.Concat("(", string.Join(", ", parameters), ")");

            // Example:
            // FooBar(int, bool, char[])
            return string.Concat(name, parmsString);
        }

        /// <summary>
        /// Generates the class declaration suppression message.
        /// </summary>
        /// <param name="node">The node.</param>
        private static string GenerateSuppressionMessageClassDeclaration(
            SyntaxNode node) {
            var classDeclaration = node.GetAncestorOfType<ClassDeclarationSyntax>();

            return classDeclaration?.Identifier.Text;
        }

        /// <summary>
        /// Generates the property declaration suppression message.
        /// </summary>
        /// <param name="node">The node.</param>
        private static string GenerateSuppressionMessagePropertyDeclaration(
            SyntaxNode node) {
            var propertyDeclaration = node.GetAncestorOfType<PropertyDeclarationSyntax>();

            return propertyDeclaration?.Identifier.Text;
        }

        /// <summary>
        /// Generates the namespace suppression message.
        /// </summary>
        /// <param name="node">The node.</param>
        private static string GenerateSuppressionMessageNamespace(
            SyntaxNode node) {
            var namespaceDeclaration = node.GetAncestorOfType<NamespaceDeclarationSyntax>();

            return namespaceDeclaration?.Name.ToString();
        }

#if AUTOSUPPRESS
        /// <summary>
        /// Sets the suppressions file's contents.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="contents">The contents.</param>
        /// <param name="encoding">The encoding.</param>
        private static void SetFileContents(string path, string[] contents, Encoding encoding) {
            try {
                File.WriteAllLines(path, contents, encoding);
            } catch (Exception) {
                // Do nothing.
            }
        }

        /// <summary>
        /// Adds the suppressions file to the project.
        /// </summary>
        /// <param name="data">The date.</param>
        private static void AddSuppressionsFileToProject(NodeData data) {
            const string suppressionsWhitespace = "    ";
            const string suppressionsContent = "<Compile Include=\"GlobalSuppressions.cs\" />";

            // Sanity check.
            if (string.IsNullOrWhiteSpace(data.ProjectPath) || string.IsNullOrWhiteSpace(data.ProjectName)) {
                return;
            }

            // Get the project's path.
            string project = Path.Combine(data.ProjectPath, data.ProjectName);

            string[] content;

            // Attempt to retrieve content from the project file.
            try {
                content = File.ReadAllLines(project);
            } catch (Exception) {
                // Ignore any projects that raise exceptions.
                return;
            }

            // If the suppressions file is already included in the project...
            if (content.Any(line => line.Contains(suppressionsContent))) {
                // ...exit early.
                return;
            }

            // Locate the index of the first compiled file, referenced within the project file.
            int index = -1;
            for (int i = 0; i < content.Length; ++i) {
                if (!content[i].Contains("<Compile Include=\"")) {
                    continue;
                }

                index = i;
                break;
            }

            // If the index was not found...
            if (index < 0) {
                // ...ignore this project file.
                return;
            }

            // Create a new content array that is one size larger than the original.
            var newContent = new string[content.Length + 1];

            // Set the indexed location to be the new suppressions file content.
            newContent[index] = string.Concat(suppressionsWhitespace, suppressionsContent);

            // Iterate through the new content container, adding the old content into the new container.
            for (int i = 0; i < newContent.Length; ++i) {
                // If the current iteration is above the located index,
                // access an earlier index from the old content (which lacks the new addition).
                if (i < index) {
                    newContent[i] = content[i];
                } else if (i > index) {
                    newContent[i] = content[i - 1];
                }
            }

            // Attempt to write the newly organised content to the project file.
            try {
                File.WriteAllLines(project, newContent, Encoding.UTF8);
            } catch (Exception) {
                // Do nothing.
            }
        }
#endif

        #endregion Methods: Auto Suppression
    }
}
