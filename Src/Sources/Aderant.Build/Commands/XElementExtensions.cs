using System;
using System.Linq;
using System.Xml.Linq;

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

    public static XElement GetOrAddElement(this XElement element, XName name) {
        var section = element.Descendants(name).SingleOrDefault();
        if (section == null) {
            section = new XElement(name);
            element.Add(section);
        }
        return section;
    }

    internal static T GetAttributeValue<T>(XElement element, string attributeName) where T : class {
        T result;
        try {
            var attribute = element.Attribute(attributeName);
            if (attribute != null) {
                result = (T)((object)attribute.Value);
            } else {
                result = default(T);
            }
        } catch (Exception ex) {
            throw new InvalidOperationException("Unable to get or set attribute: " + attributeName, ex);
        }
        return result;
    }
}