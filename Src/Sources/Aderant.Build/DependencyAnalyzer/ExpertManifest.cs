using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.Providers;

namespace Aderant.Build.DependencyAnalyzer {
    internal class ExpertManifest : IModuleProvider, IGlobalAttributesProvider {
        const LoadOptions loadOptions = LoadOptions.SetBaseUri | LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace;

        private readonly IFileSystem2 fileSystem;
        private readonly XDocument manifest;


        private List<ExpertModule> modules;

        // The directory which contains the modules. Determined by the path to the manifest by convention.
        private string moduleDirectory;

        // The dependencies manifests this instance is bound to.
        private IEnumerable<DependencyManifest> dependencyManifests;

        /// <summary>
        /// Gets the product manifest path.
        /// </summary>
        /// <value>
        /// The product manifest path.
        /// </value>
        public string ProductManifestPath {
            get { return new Uri(manifest.BaseUri).LocalPath; }
        }

        /// <summary>
        /// Gets the two part branch name
        /// </summary>
        /// <value>
        /// The branch.
        /// </value>
        public string Branch { get; private set; }

        /// <summary>
        /// Gets the dependency manifests.
        /// </summary>
        /// <value>
        /// The dependency manifests.
        /// </value>
        public IEnumerable<DependencyManifest> DependencyManifests {
            get {
                if (dependencyManifests == null) {
                    throw new InvalidOperationException("This instance is not bound to a specific set of dependency manifests");
                }

                return dependencyManifests;
            }
            protected set { dependencyManifests = value; }
        }

        public bool HasDependencyManifests {
            get { return dependencyManifests != null; }
        }

        public string ModulesDirectory {
            get { return moduleDirectory; }
            set {
                moduleDirectory = value;
                if (string.IsNullOrEmpty(Branch)) {
                    Branch = PathHelper.GetBranch(value);
                }
            }
        }


        /// <summary>
        /// Gets the distinct complete list of available modules and those referenced in Dependency Manifests.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ExpertModule> GetAll() {
            return modules.ToList();
        }

        /// <summary>
        /// Tries to get the Dependency Manifest document from the given module.  
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <param name="manifest">The manifest.</param>
        public bool TryGetDependencyManifest(string moduleName, out DependencyManifest manifest) {
            if (dependencyManifests != null) {
                manifest = dependencyManifests.FirstOrDefault(m => string.Equals(m.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase));

                if (manifest != null) {
                    return true;
                }
            } else {
                string modulePath = Path.Combine(moduleDirectory, moduleName);

                if (fileSystem.DirectoryExists(modulePath)) {
                    if (fileSystem.GetFiles(modulePath, DependencyManifest.DependencyManifestFileName, true).FirstOrDefault() != null) {
                        manifest = DependencyManifest.LoadFromModule(modulePath);
                        return true;
                    }
                }
            }

            manifest = null;
            return false;
        }

        /// <summary>
        /// Tries to the get the path to the dependency manifest for a given module.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <param name="manifestPath">The manifest path.</param>
        /// <returns></returns>
        public bool TryGetDependencyManifestPath(string moduleName, out string manifestPath) {
            string dependencyManifest = fileSystem.GetFiles(Path.Combine(moduleDirectory, moduleName), DependencyManifest.DependencyManifestFileName, true).FirstOrDefault();

            if (dependencyManifest != null) {
                manifestPath = dependencyManifest;
                return true;
            }

            manifestPath = null;
            return false;
        }

        ///<summary>
        /// Determines whether the specified module is available to the current branch.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <returns>
        ///   <c>true</c> if the specified module name is available; otherwise, <c>false</c>.
        /// </returns>
        public bool IsAvailable(string moduleName) {
            ModuleType moduleType = ExpertModule.GetModuleType(moduleName);

            if (moduleType == ModuleType.ThirdParty || moduleType == ModuleType.Help) {
                return fileSystem.DirectoryExists(Path.Combine(moduleDirectory, "ThirdParty", moduleName, "bin"));
            }

            return fileSystem.FileExists(Path.Combine(moduleDirectory, moduleName, "Build", "TFSBuild.proj"));
        }

        /// <summary>
        /// Gets the module with the specified name.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <returns></returns>
        public ExpertModule GetModule(string moduleName) {
            foreach (ExpertModule module in modules) {
                if (string.Equals(module.Name, moduleName, StringComparison.OrdinalIgnoreCase)) {
                    return module;
                }
            }
            return null;
        }

        public void Add(ExpertModule module) {
            ExpertModule existingModule = GetModule(module.Name);

            if (existingModule != null) {
                modules.Remove(existingModule);
            }

            modules.Add(module);
        }

        /// <summary>
        /// Saves this instance to the serialized string representation.
        /// </summary>
        /// <returns></returns>
        public string Save() {
            var mapper = new ExpertModuleMapper();
            return mapper.Save(this, manifest);
        }

        /// <summary>
        /// Removes the specified items.
        /// </summary>
        /// <param name="items">The items.</param>
        public void Remove(IEnumerable<ExpertModule> items) {
            foreach (var module in items) {
                modules.Remove(module);
            }
        }

        public static ExpertManifest Create(string manifestPath) {
            if (Path.IsPathRooted(manifestPath)) {
                var root = Path.GetDirectoryName(manifestPath);

                PhysicalFileSystem fs = new PhysicalFileSystem(root);
                return new ExpertManifest(fs, manifestPath);
            }
            throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Path {0} is not a rooted path", manifestPath));
        }

        internal ExpertManifest(IFileSystem2 fileSystem, string manifestPath) {
            this.fileSystem = fileSystem;

            using (Stream stream = fileSystem.OpenFile(manifestPath)) {
                var document = XDocument.Load(stream, loadOptions);
                this.manifest = document;
            }

            Initialize();
        }

        internal ExpertManifest(XDocument manifest) {
            this.manifest = manifest;
            Initialize();
        }

        private void Initialize() {
            this.modules = LoadAllModules().ToList();

            Branch = PathHelper.GetBranch(manifest.BaseUri, false);
        }

        private IEnumerable<ExpertModule> LoadAllModules() {
            IEnumerable<XElement> moduleElements = manifest.Root.Element("Modules").Descendants();

            return moduleElements.Select(ExpertModule.Create);
        }

        /// <summary>
        /// Loads the specified manifest.
        /// </summary>
        /// <param name="manifest">The manifest.</param>
        /// <returns></returns>
        public static ExpertManifest Load(string manifest) {
            return Load(manifest, null);
        }

        /// <summary>
        /// Loads the specified manifest and binds to the specified dependency manifest.
        /// </summary>
        /// <param name="manifest">The manifest.</param>
        /// <param name="dependencyManifests">The dependency manifests.</param>
        /// <returns></returns>
        public static ExpertManifest Load(string manifest, IEnumerable<DependencyManifest> dependencyManifests) {
            var expertManifest = LoadInternal(manifest);

            if (dependencyManifests != null) {
                expertManifest.dependencyManifests = dependencyManifests;

                foreach (DependencyManifest dependencyManifest in dependencyManifests) {
                    dependencyManifest.GlobalAttributesProvider = expertManifest;
                }
            }

            return expertManifest;
        }

        private static ExpertManifest LoadInternal(string manifest) {
            if (manifest.EndsWith(".xml")) {
                return Create(manifest);
            }

            string path = ResolveLoadFromPath(manifest);
            var loadResult = LoadInternal(path);
            loadResult.ModulesDirectory = manifest;

            return loadResult;
        }

        private static string ResolveLoadFromPath(string directory) {
            string path = directory;

            if (path.IndexOf("Modules", StringComparison.OrdinalIgnoreCase) == -1) {
                path = Path.Combine(path, "Modules");
            }

            if (!Directory.Exists(path)) {
                throw new DirectoryNotFoundException(path);
            }

            return Path.Combine(path, PathHelper.PathToProductManifest);
        }

        public virtual string GetPathToBinaries(ExpertModule expertModule, string dropPath) {
            // Find the matching Module in the ExpertManifest. The ExpertManifest module may have information detailing
            // where to get the module from like the current branch, or another branch entirely.
            ExpertModule internalModule = GetModule(expertModule.Name);

            if (internalModule != null) {
                string dropLocationDirectory = internalModule.GetPathToBinaries(dropPath);

                if (fileSystem.DirectoryExists(dropLocationDirectory)) {
                    return dropLocationDirectory;
                }
            }
            string dependentModules = "";
            foreach (var module in DependencyManifests.Where(a => a.ReferencedModules.Contains(expertModule))) {
                dependentModules += " " + module.ModuleName;
            }
            throw new BuildNotFoundException("No path to binaries found for " + expertModule.Name + ". Modules with dependencies:" + dependentModules);
        }

        public XElement MergeAttributes(XElement element) {
            var entry = manifest.Root.Element("Modules")
                .Descendants()
                .FirstOrDefault(m => string.Equals(m.Attribute("Name").Value, element.Attribute("Name").Value, StringComparison.OrdinalIgnoreCase));

            if (entry != null) {
                var mergedAttributes = MergeAttributes(entry.Attributes(), element.Attributes());
                element.ReplaceAttributes(mergedAttributes);
            }

            return element;
        }

        private IEnumerable<XAttribute> MergeAttributes(IEnumerable<XAttribute> productManifestAttributes, IEnumerable<XAttribute> otherAttributes) {
            List<XAttribute> mergedAttributes = new List<XAttribute>(productManifestAttributes);
            foreach (var otherAttribute in otherAttributes) {
                bool add = true;

                foreach (var attribute in mergedAttributes) {
                    if (string.Equals(otherAttribute.Name.LocalName, attribute.Name.LocalName, StringComparison.OrdinalIgnoreCase)) {
                        add = false;

                        if (!string.Equals(otherAttribute.Value, attribute.Value)) {
                            attribute.Value = otherAttribute.Value;
                        }
                    }
                }

                if (add) {
                    mergedAttributes.Add(otherAttribute);
                }
            }


            if (mergedAttributes.Count(attr => string.Equals(attr.Name.LocalName, "Name")) != 1) {
                throw new InvalidOperationException("Invalid element merge. Elements must have the same \"Name\" value");
            }

            return mergedAttributes;
        }
    }
}