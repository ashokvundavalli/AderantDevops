using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Aderant.Build.Analyzer.Extensions {
    /// <summary>
    /// Extension methods for Roslyn types.
    /// </summary>
    public static class RoslynExtensions {
        #region Types

        /// <summary>
        /// Data bucket containing pertinent information about an Analysis Context.
        /// </summary>
        public struct NodeData {
            public NodeData(SyntaxNode node, string projectName, string projectPath) {
                if (node == null ||
                    string.IsNullOrWhiteSpace(projectName) ||
                    string.IsNullOrWhiteSpace(projectPath)) {
                    Node = null;
                    ProjectName = null;
                    ProjectPath = null;
                    SuppressionFilePath = null;

                    return;
                }

                Node = node;
                ProjectName = projectName;
                ProjectPath = projectPath;
                SuppressionFilePath = Path.Combine(projectPath, "GlobalSuppressions.cs");
            }

            public SyntaxNode Node { get; }

            public string ProjectName { get; }

            public string ProjectPath { get; }

            public string SuppressionFilePath { get; }
        }

        #endregion Types

        #region Methods: Auto Suppression

        /// <summary>
        /// Gets the <seealso cref="NodeData" /> for the current <seealso cref="SyntaxNode" />.
        /// </summary>
        /// <param name="node">The node.</param>
        public static NodeData GetNodeData(this SyntaxNode node) {
            string path, name;
            GetProjectData(node.SyntaxTree.FilePath, out path, out name);

            return new NodeData(node, name, path);
        }

        /// <summary>
        /// Gets the current project's path.
        /// </summary>
        /// <param name="nodeFilePath">The node file path.</param>
        /// <param name="path">The path.</param>
        /// <param name="name">The name.</param>
        private static void GetProjectData(string nodeFilePath, out string path, out string name) {
            path = null;
            name = null;

            // If the node's file does not exist, or is not rooted (I.E. a UnitTest), return.
            if (!File.Exists(nodeFilePath) || !Path.IsPathRooted(nodeFilePath)) {
                return;
            }

            // Get the parent directory of the node's file.
            string directoryName = Path.GetDirectoryName(nodeFilePath);

            // Sanity check to confirm the file is in a directory.
            if (string.IsNullOrWhiteSpace(directoryName)) {
                return;
            }

            // Create a dicretory object to represent the parent directory of the node's file.
            var directory = new DirectoryInfo(directoryName);

            // Iterate through the directory hierarchy.
            while (directory != null) {
                // Get all 'csproj' files in the directory.
                var files = directory.GetFiles("*.csproj");

                // Iterate through each retrieved file.
                foreach (var file in files) {
                    // Get the contents of the project file.
                    string[] fileContents;
                    try {
                        fileContents = File.ReadAllLines(file.FullName);
                    } catch (Exception) {
                        // Ignore any files that raise exceptions.
                        continue;
                    }

                    // Iterate through each line of the file's contents.
                    foreach (var line in fileContents) {
                        // Split each line into chunks by the " character.
                        // Example:
                        //      <Compile Include="Areas\Foo\Bar\FooBar.cs" />
                        // Becomes:
                        //      strings[0] = <Compile Include=
                        //      strings[1] = Areas\Foo\Bar\FooBar.cs
                        //      strings[2] =  />
                        var strings = line.Split('"');

                        // If the number of 'chunks' is not 3, the current line can be immediatly ignored.
                        // Any 'Compile' element will have exactly 3 chunks.
                        if (strings.Length != 3) {
                            continue;
                        }

                        // If the node's filepath does not end with the central 'chunk', ignore this line.
                        if (!nodeFilePath.EndsWith(strings[1])) {
                            continue;
                        }

                        // Set the path and name data.
                        path = directory.FullName;
                        name = file.Name;

                        return;
                    }
                }

                // Set the parent as the current directory, stepping up through the hierarchy.
                directory = directory.Parent;
            }
        }

        #endregion Methods: Auto Suppression

        #region Methods: Syntax Nodes

        /// <summary>
        /// Gets the type of the ancestor of.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="node">The node.</param>
        public static T GetAncestorOfType<T>(this SyntaxNode node) {
            return node.Ancestors().OfType<T>().FirstOrDefault();
        }

        /// <summary>
        /// Gets the type of the ancestor or self of.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="node">The node.</param>
        public static T GetAncestorOrSelfOfType<T>(this SyntaxNode node) {
            return node.AncestorsAndSelf().OfType<T>().FirstOrDefault();
        }

        #endregion Methods: Syntax Nodes
    }
}
