using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Aderant.Build.DependencyAnalyzer {
    public class DependencyManifest {
        private readonly XDocument manifest;
        private List<ExpertModule> referencedModules;

        internal DependencyManifest(string moduleName, XDocument manifest) {
            this.ModuleName = moduleName;
            this.manifest = manifest;

            if (manifest.Root != null) {
                XAttribute attribute = manifest.Root.Attribute("IsEnabled");
                if (attribute != null) {
                    bool value;
                    if (bool.TryParse(attribute.Value, out value)) {
                        IsEnabled = value;
                        return;
                    }
                }
            }

            this.IsEnabled = true;
        }

        public bool IsEnabled { get; private set; }

        /// <summary>
        /// The dependency manifest file file.
        /// </summary>
        internal const string DependencyManifestFileName = "DependencyManifest.xml";

        /// <summary>
        /// The relative path to the dependency manifest file from the module directory.
        /// </summary>
        internal const string PathToDependencyManifestFile = @"Build\" + DependencyManifestFileName;

        private const LoadOptions LoadOptions = System.Xml.Linq.LoadOptions.SetBaseUri | System.Xml.Linq.LoadOptions.SetLineInfo;

        /// <summary>
        /// Gets the name of the module which this manifest is for.
        /// </summary>
        /// <value>
        /// The name of the module.
        /// </value>
        public string ModuleName { get; private set; }

        /// <summary>
        /// Gets the referenced modules.
        /// </summary>
        /// <value>
        /// The referenced modules.
        /// </value>
        /// <exception cref="Aderant.Build.DuplicateModuleInManifestException"></exception>
        public IList<ExpertModule> ReferencedModules {
            get {
                if (referencedModules == null) {
                    referencedModules = new List<ExpertModule>();

                    var elements = manifest.Descendants("ReferencedModule");

                    foreach (var element in elements) {
                        var mergedElement = element;

                        if (GlobalAttributesProvider != null) {
                            mergedElement = GlobalAttributesProvider.MergeAttributes(mergedElement);
                        }

                        var module = ExpertModule.Create(mergedElement);

                        if (referencedModules.Contains(module)) {
                            throw new DuplicateModuleInManifestException(string.Format(CultureInfo.InvariantCulture, "The module {0} appears more than once in {1}", module.Name, manifest.BaseUri));
                        }

                        referencedModules.Add(module);
                    }
                }
                return referencedModules;
            }
        }

        internal IGlobalAttributesProvider GlobalAttributesProvider { get; set; }

        /// <summary>
        /// Loads a dependency manifest from the given module directory.
        /// </summary>
        /// <param name="modulePath">The module path.</param>
        /// <returns></returns>
        public static DependencyManifest LoadFromModule(string modulePath) {
            DependencyManifest dependencyManifest;
            if (TryLoadFromModule(modulePath, out dependencyManifest)) {
                return dependencyManifest;
            }

            throw new InvalidOperationException("Could not load a DependencyManifest from: " + modulePath);
        }

        /// <summary>
        /// Recursively loads all dependency manifests.
        /// </summary>
        /// <param name="fs">The fs.</param>
        /// <param name="modulesRootPath">The modules root path.</param>
        public static IList<DependencyManifest> LoadAll(FileSystem fs, string modulesRootPath) {
            string[] directories = fs.Directory.GetDirectories(modulesRootPath);

            IList<DependencyManifest> manifests = new List<DependencyManifest>(directories.Length);

            foreach (var directory in directories) {
                DependencyManifest dependencyManifest;
                if (TryLoadFromModule(directory, out dependencyManifest)) {
                    manifests.Add(dependencyManifest);
                }
            }

            return manifests;
        }

        private static bool TryLoadFromModule(string modulePath, out DependencyManifest manifest) {
            try {
                manifest = LoadFromFile(Path.Combine(modulePath, PathToDependencyManifestFile), modulePath);
                return true;
            } catch {
                manifest = null;
                return false;
            }
        }

        private static DependencyManifest LoadFromFile(string path, string modulePath) {
            var manifest = new DependencyManifest(Path.GetFileName(modulePath), XDocument.Load(path, LoadOptions));
            return manifest;
        }

        internal static DependencyManifest Parse(string moduleName, string text) {
            var manifest = new DependencyManifest(moduleName, XDocument.Parse(text, LoadOptions));

            return manifest;
        }

        /// <summary>
        /// Saves this instance and returns the serialized string representation.
        /// </summary>
        /// <returns></returns>
        public string Save() {
            ExpertModuleMapper mapper = new ExpertModuleMapper();
            return mapper.Save(this, manifest);
        }
    }

    internal interface IGlobalAttributesProvider {
        XElement MergeAttributes(XElement element);
    }
}