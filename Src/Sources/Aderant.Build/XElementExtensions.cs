using System;
using System.Xml;
using System.Xml.Linq;

namespace Aderant.Build {
    internal static class XElementExtensions {

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

    }
}
