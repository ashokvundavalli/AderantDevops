using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.Providers;

namespace Aderant.Build.DependencyAnalyzer {

    internal class ExpertManifest : IModuleProvider {
        private readonly FileSystem fileSystem;
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
            get {
                return new Uri(manifest.BaseUri).LocalPath;
            }
        }

        /// <summary>
        /// Gets the two part branch name
        /// </summary>
        /// <value>
        /// The branch.
        /// </value>
        public string Branch {
            get { return PathHelper.GetBranch(manifest.BaseUri); }
        }

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
            protected set {
                dependencyManifests = value;
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
        /// <returns></returns>
        public bool TryGetDependencyManifest(string moduleName, out DependencyManifest manifest) {
            if (dependencyManifests != null) {
                manifest = dependencyManifests.FirstOrDefault(m => string.Equals(m.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase));

                if (manifest != null) {
                    return true;
                }
            } else {
                string modulePath = Path.Combine(moduleDirectory, moduleName);
                if (fileSystem.Directory.Exists(modulePath)) {
                    if (fileSystem.File.Exists(Path.Combine(modulePath, DependencyManifest.PathToDependencyManifestFile))) {
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
            string dependencyManifest = Path.Combine(moduleDirectory, moduleName, DependencyManifest.PathToDependencyManifestFile);

            if (fileSystem.File.Exists(dependencyManifest)) {
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

            if (moduleType == ModuleType.ThirdParty) {
                return fileSystem.Directory.Exists(Path.Combine(moduleDirectory, "ThirdParty", moduleName, "bin"));
            }

            return fileSystem.File.Exists(Path.Combine(moduleDirectory, moduleName, "Build", "TFSBuild.proj"));
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

        protected ExpertManifest(XDocument manifest) : this(FileSystem.Default, manifest) {
            
        }

        internal ExpertManifest(FileSystem fileSystem, XDocument manifest) {
            this.fileSystem = fileSystem;
            this.manifest = manifest;
            
            // Determine the modules directory
            if (!string.IsNullOrEmpty(manifest.BaseUri)) {
                // Manifest lives in the package folder: C:\tfs\ExpertSuite\Dev\Framework\Modules\Build.Infrastructure\Src\Package

                string localPath = new Uri(manifest.BaseUri).LocalPath;
                moduleDirectory = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(localPath), @"..\..\..")); // A bit yucky
            }

            this.modules = LoadAllModules().ToList();
        }

        private IEnumerable<ExpertModule> LoadAllModules() {
            XDocument expertManifest = manifest;
            IEnumerable<XElement> moduleElements = expertManifest.Root.Element("Modules").Descendants();

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
            }

            return expertManifest;
        }

        private static ExpertManifest LoadInternal(string manifest) {
            const LoadOptions loadOptions = (LoadOptions.SetBaseUri | LoadOptions.SetLineInfo);
            try {
                if (manifest.EndsWith(".xml")) {
                    return new ExpertManifest(XDocument.Load(manifest, loadOptions));
                }

                string path = ResolveLoadFromPath(manifest);
                return new ExpertManifest(XDocument.Load(path, loadOptions));
            } catch (ArgumentException) {
                return new ExpertManifest(XDocument.Parse(manifest, loadOptions));
            }
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

                if (fileSystem.Directory.Exists(dropLocationDirectory)) {
                    return dropLocationDirectory;
                }
            }

            throw new BuildNotFoundException("No path to binaries found for " + expertModule.Name);
        }
    }
}