using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.DependencyResolver.Parser {
    internal class DependencyManifestParser {
        public static DependencyManifest Parse(string text) {

            var doc = XDocument.Parse(text);

            var firstNode = doc.Root;
            if (firstNode != null) {
                if (firstNode.Descendants("dependencies").Any() || firstNode.Attribute("version") != null) {
                    return ParseV2(firstNode);
                }
            }

            throw new NotImplementedException();
        }

        private static DependencyManifest ParseV2(XElement doc) {
            var requirements = ParseElement(doc);

            DependencyManifest manifest =  new DependencyManifest();
            foreach (IDependencyRequirement requirement in requirements) {
                manifest.AddRequirement(requirement);
            }

            return manifest;
        }

        private static IEnumerable<IDependencyRequirement> ParseElement(XElement doc) {
            var dependendenciesElement = doc.Element("dependencies");

            if (dependendenciesElement != null) {
                foreach (var dependencyElement in dependendenciesElement.Descendants("dependency")) {

                    var name = dependencyElement.Element("artifactId")?.Value;
                    var type = dependencyElement.Element("type")?.Value;
                    var scope = dependencyElement.Element("scope")?.Value;

                    if (name != null) {
                        yield return DependencyRequirement.Create(name, scope);
                    }
                }
            }
        }
    }
}
