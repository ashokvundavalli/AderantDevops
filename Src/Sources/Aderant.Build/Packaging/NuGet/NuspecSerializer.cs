using System;
using System.Linq;
using System.Xml.Linq;

namespace Aderant.Build.Packaging.NuGet {
    internal class NuspecSerializer {
        private readonly string text;
        private readonly Nuspec nuspec;

        public NuspecSerializer(string text, Nuspec nuspec) {
            this.text = text;
            this.nuspec = nuspec;
        }

        public static string GetVersion(string text) {
            XDocument document = XDocument.Parse(text);

            var version = GetElementValue("version", document);

            return version.Value;
        }

        private static XElement GetElementValue(string elementName, XDocument document) {
            return document.Descendants().First(d => String.Equals(d.Name.LocalName, elementName, StringComparison.OrdinalIgnoreCase));
        }

        public void Deserialize() {
            XDocument document = XDocument.Parse(text);

            nuspec.Id = new StringNuspecValue { Value = GetElementValue("id", document).Value };
            nuspec.Version = new StringNuspecValue { Value = GetElementValue("version", document).Value };
            nuspec.Description = new StringNuspecValue { Value = GetElementValue("description", document).Value };
        }

        public static string Serialize(Nuspec nuspec, string text) {
            XDocument document = XDocument.Parse(text);

            GetElementValue("id", document).Value = nuspec.Id.Value;
            GetElementValue("version", document).Value = nuspec.Version.Value;
            GetElementValue("description", document).Value = nuspec.Description.Value;

            return document.ToString();
        }
    }
}