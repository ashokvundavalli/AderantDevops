using System;
using System.Linq;
using System.Xml;
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

        public static bool TryGetValueFromAttribute<T>(this XElement element, string attributeName, out T output, T defaultValue) {
            try {
                XAttribute xAttribute = element.Attribute(attributeName);

                if (xAttribute == null) {
                    output = defaultValue;
                    return false;
                }

                if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?)) {
                    object value = XmlConvert.ToBoolean(xAttribute.Value);
                    output = (T)value;

                    return true;
                }

                output = (T)Convert.ChangeType(xAttribute.Value, typeof(T));
                return true;
            } catch {
                output = defaultValue;
                return false;
            }
        }

        public static bool TryGetValueFromAttribute<T>(this XElement element, string attributeName, out T output) where T : new() {
            return TryGetValueFromAttribute(element, attributeName, out output, new T());
        }
    }
}