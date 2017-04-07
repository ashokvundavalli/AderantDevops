using System.Linq;
using System.Xml.Linq;

namespace Aderant.Build {
    internal static class XElementExtensions {

        /// <summary>
        /// Gets the or add element with the specified name. Optionally accepts a node path e.g. foo/bar/baz.
        /// </summary>
        /// <param name="content">The content.</param>
        /// <param name="name">The name.</param>
        /// <returns></returns>
        public static XElement GetOrAddElement(this XElement content, string name) {
            var parts = name.Split('/');

            var root = content;

            foreach (string element in parts) {
                var descendant = root.Descendants(element).FirstOrDefault();
                if (descendant == null) {
                    descendant = new XElement(element);
                    root.Add(descendant);
                }
                root = descendant;
            }

            return root;
        }
    }
}