using System;
using System.Collections.Generic;
using System.Linq;

namespace Aderant.Build.Utilities {
    internal class TreePrinter {

        private const string Cross = " ├─";
        private const string Corner = " └─";
        private const string Vertical = " │ ";
        private const string Space = "   ";

        public static void Print(List<Node> topLevelNodes, Action<string> printAction) {
            foreach (var node in topLevelNodes) {
                PrintNode(printAction, node, indent: "");
            }
        }

        static void PrintNode(Action<string> printAction, Node node, string indent) {
            printAction(node.Name);
            printAction(Environment.NewLine);

            if (node.Children != null) {
                var numberOfChildren = node.Children.Count;

                for (var i = 0; i < numberOfChildren; i++) {
                    var child = node.Children[i];
                    var isLast = (i == (numberOfChildren - 1));
                    PrintChildNode(printAction, child, indent, isLast);
                }
            }
        }

        static void PrintChildNode(Action<string> printAction, Node node, string indent, bool isLast) {
            // Print the provided pipes/spaces indent
            printAction(indent);

            // Depending if this node is a last child, print the
            // corner or cross, and calculate the indent that will
            // be passed to its children
            if (isLast) {
                printAction(Corner);
                indent += Space;
            } else {
                printAction(Cross);
                indent += Vertical;
            }

            PrintNode(printAction, node, indent);
        }

        internal class Node {
            public string Name { get; set; }

            public List<Node> Children { get; set; } = new List<Node>();

            internal void AddChild(string name, IEnumerable<Node> children = null)  {
                Children.Add(new Node { Name = name, Children = new List<Node>(children ?? Enumerable.Empty<Node>()) });
            }
        }
    }
}
