using System;
using System.Linq;
using System.Xml.Linq;

namespace Aderant.Build.Packaging {
    internal class NuspecParser {
        public static string GetVersion(string text) {
            XDocument document = XDocument.Parse(text);

            var version = document.Descendants().First(d => String.Equals(d.Name.LocalName, "version", StringComparison.OrdinalIgnoreCase));

            return version.Value;
        }
    }
}