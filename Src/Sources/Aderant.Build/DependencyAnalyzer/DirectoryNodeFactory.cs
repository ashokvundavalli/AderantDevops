using System;
using System.Collections.Concurrent;
using System.Linq;
using Aderant.Build.DependencyAnalyzer.Model;
using Aderant.Build.MSBuild;
using Aderant.Build.ProjectSystem;

namespace Aderant.Build.DependencyAnalyzer {
    internal class DirectoryNodeFactory {
        private ConcurrentDictionary<string, DirectoryNode[]> nodeMap = new ConcurrentDictionary<string, DirectoryNode[]>();

        private ResponseFileParser responseFileParser;

        public DirectoryNodeFactory(IFileSystem fileSystem) {
            this.responseFileParser = new ResponseFileParser(fileSystem);
        }

        /// <summary>
        /// Seeds the factory with an existing graph.
        /// </summary>
        public void Initialize(DependencyGraph graph) {
            var nodes = graph.Nodes.OfType<DirectoryNode>();

            foreach (var node in nodes) {
                if (!nodeMap.TryGetValue(node.DirectoryName, out var nodePair)) {
                    nodePair = NewNodePairArray();
                    nodeMap[node.DirectoryName] = nodePair;
                }

                if (node.IsPostTargets) {
                    EndNodeElement(nodePair) = node;
                } else {
                    StartNodeElement(nodePair) = node;
                }
            }
        }

        private static DirectoryNode[] NewNodePairArray() {
            return new DirectoryNode[2];
        }

        private static ref DirectoryNode StartNodeElement(DirectoryNode[] nodePair) {
            return ref nodePair[0];
        }

        private static ref DirectoryNode EndNodeElement(DirectoryNode[] nodePair) {
            return ref nodePair[1];
        }

        public Tuple<DirectoryNode, DirectoryNode> Create(DependencyGraph graph, string nodeName, string nodeFullPath) {

            DirectoryNode[] UpdateValueFactory(string key, DirectoryNode[] pair) {
                // Create a new node that represents the start of a directory
                var startNode = StartNodeElement(pair);
                if (startNode == null) {
                    startNode = new DirectoryNode(nodeName, nodeFullPath, false);
                    StartNodeElement(pair) = startNode;
                }

                // Create a new node that represents the completion of a directory
                var endNode = EndNodeElement(pair);
                if (endNode == null) {
                    endNode = new DirectoryNode(nodeName, nodeFullPath, true);
                    EndNodeElement(pair) = endNode;
                }

                endNode.AddResolvedDependency(null, startNode);

                InitializeNode(startNode.Directory, startNode);

                return pair;
            }

            var result = nodeMap.AddOrUpdate(
                nodeName,
                key => {
                    var nodes = UpdateValueFactory(key, NewNodePairArray());

                    graph.Add(StartNodeElement(nodes));
                    graph.Add(EndNodeElement(nodes));

                    return nodes;
                },
                UpdateValueFactory);

            return Return(result);
        }

        private static Tuple<DirectoryNode, DirectoryNode> Return(DirectoryNode[] result) {
            return Tuple.Create(StartNodeElement(result), EndNodeElement(result));
        }

        private void InitializeNode(string nodeFullPath, DirectoryNode directoryNode) {
            if (directoryNode.TextTransformEnabled != null) {
                return;
            }

            if (!string.IsNullOrEmpty(nodeFullPath)) {
                var file = ResponseFileParser.CreatePath(nodeFullPath);
                PropertyList propertyList = responseFileParser.ParseFile(file);

                if (propertyList != null && propertyList.ContainsKey("T4TransformEnabled")) {
                    var enabled = propertyList["T4TransformEnabled"];

                    if (!string.IsNullOrWhiteSpace(enabled)) {
                        if (string.Equals(enabled.Trim(), bool.TrueString, StringComparison.OrdinalIgnoreCase)) {
                            directoryNode.TextTransformEnabled = true;
                            return;
                        }
                    }
                }
            }

            directoryNode.TextTransformEnabled = false;
        }
    }
}