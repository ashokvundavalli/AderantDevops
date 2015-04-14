using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Aderant.Build.DependencyAnalyzer {
    internal class ExpertModuleMapper {
        internal XElement Save(IEnumerable<ExpertModule> modules, bool isProductManifest) {
            modules = SortManifestNodesByName(modules);

            XElement root = new XElement("Modules");

            if (isProductManifest) {
                root = new XElement("Modules");
            } else {
                root = new XElement("ReferencedModules");
            }

            foreach (ExpertModule module in modules) {
                var moduleElement = new XElement(isProductManifest ? "Module" : "ReferencedModule", new XAttribute("Name", module.Name));

                // Assembly information should only go into the product manifest as the product manifest defines what version of a dependency is used
                // but we will keep the data around for backwards compatibility
                if (module.ModuleType != ModuleType.ThirdParty && !string.IsNullOrEmpty(module.AssemblyVersion)) {
                    moduleElement.Add(new XAttribute("AssemblyVersion", module.AssemblyVersion));
                }

                if (isProductManifest) {
                    if (module.GetAction != GetAction.None) {
                        moduleElement.Add(new XAttribute("GetAction", module.GetAction.ToString().ToLowerInvariant()));
                    }

                    if (!string.IsNullOrEmpty(module.Branch)) {
                        moduleElement.Add(new XAttribute("Path", module.Branch));
                    }
                }

                IEnumerable<XAttribute> customAttributes = module.CustomAttributes;
                if (customAttributes != null) {
                    foreach (XAttribute attribute in customAttributes) {
                        moduleElement.Add(attribute);
                    }
                }

                root.Add(moduleElement);
            }

            return root;
        }

        public string Save(ExpertManifest expertManifest, XDocument manifest) {
            XElement modules = Save(expertManifest.GetAll(), true);

            ReplaceElement(manifest, modules, "Modules");

            return SaveDocument(manifest);
        }

        private static void ReplaceElement(XDocument manifest, XElement modules, string elementName) {
            if (manifest.Root != null) {
                XElement element = manifest.Root.Element(elementName);
                if (element != null) {
                    element.Remove();
                }
                manifest.Root.Add(modules);
            }
        }

        public string Save(DependencyManifest dependencyManifest, XDocument manifest) {
            XElement modules = Save(dependencyManifest.ReferencedModules, false);

            ReplaceElement(manifest, modules, "ReferencedModules");

            return SaveDocument(manifest);
        }

        private string SaveDocument(XDocument document) {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = "    ";
            settings.NewLineOnAttributes = false;
            settings.Encoding = Encoding.UTF8;

            using (XmlStringWriter builder = new XmlStringWriter(new StringBuilder())) {
                using (XmlWriter writer = XmlWriter.Create(builder, settings)) {
                    document.Save(writer);
                }

                return builder.ToString();
            }
        }

        private static IEnumerable<ExpertModule> SortManifestNodesByName(IEnumerable<ExpertModule> modules) {
            return modules
                .OrderBy(m => m.Name)
                .ThenByDescending(m => m.AssemblyVersion)
                .ThenByDescending(m => m.GetAction)
                .ThenByDescending(m => m.Branch)
                .ToList();
        }

        private class XmlStringWriter : StringWriter {
            public XmlStringWriter(StringBuilder sb)
                : base(sb) {
            }

            public override Encoding Encoding {
                get { return Encoding.UTF8; }
            }
        }
    }
}