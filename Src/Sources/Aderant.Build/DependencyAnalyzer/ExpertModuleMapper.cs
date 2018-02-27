using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Aderant.Build.DependencyAnalyzer {
    internal class ExpertModuleMapper {
        public static ExpertModule MapFrom(XElement element, ExpertModule expertModule, out IList<XAttribute> customAttributes) {
            var mapper = new Mapper(element);
            mapper.Map(expertModule);

            customAttributes = mapper.CustomAttributes;

            return expertModule;
        }

        internal XElement Save(IEnumerable<ExpertModule> modules, bool isProductManifest) {
            modules = SortManifestNodesByName(modules);

            XElement root = new XElement("Modules");

            if (isProductManifest) {
                root = new XElement("ProductManifest", new XAttribute("Name", "Expert"), new XAttribute("ExpertVersion", ""));
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

    internal class Mapper {
        private readonly XElement element;
        private List<XAttribute> customAttributes;

        public Mapper(XElement element) {
            this.element = element;
            this.customAttributes = element.Attributes().ToList();
        }

        public IList<XAttribute> CustomAttributes {
            get {
                // Return a copy so we don't leak this instance.
                return customAttributes.ToList();
            }
        }

        public void Map(ExpertModule expertModule) {
            SetPropertyValue("Name", value => expertModule.Name = value);

            SetPropertyValue("AssemblyVersion", value => expertModule.AssemblyVersion = value);

            SetPropertyValue("FileVersion", value => expertModule.FileVersion = value);

            SetPropertyValue("Path", value => expertModule.Branch = value);

            SetPropertyValue("Extract", value => expertModule.Extract = ToBoolean(value));

            SetPropertyValue("Target", value => expertModule.Target = value);

            SetPropertyValue("PackageType", value => ParseEnum<PackageType>(value));

            SetPropertyValue("GetAction", value => {

                if (!string.IsNullOrEmpty(value)) {
                    expertModule.GetAction = (GetAction)Enum.Parse(typeof(GetAction), value.Replace("_", "-"), true);
                }

            });

            if (expertModule.ModuleType == ModuleType.ThirdParty) {
            }

            if (expertModule.GetAction == GetAction.NuGet) {
            }
        }

        private static TEnum ParseEnum<TEnum>(string value) where TEnum : struct {
            TEnum result;
            if (Enum.TryParse(value, out result)) {
                return result;
            }
            return result;
        }

        private static bool ToBoolean(string value) {
            bool result;
            if (bool.TryParse(value, out result)) {
                return result;
            }
            return false;
        }

        private void SetPropertyValue(string attributeName, Action<string> setAction) {
            XAttribute attribute = element.Attribute(attributeName);
            if (attribute != null) {
                setAction(attribute.Value);

                // Remove this attribute from the collection as it isn't custom
                customAttributes.Remove(attribute);
            }
        }
    }

    public enum PackageType {
        Binaries,
        Side,
    }
}