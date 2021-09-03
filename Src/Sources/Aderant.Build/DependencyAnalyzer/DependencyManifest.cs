using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.DependencyResolver;

namespace Aderant.Build.DependencyAnalyzer {

    [DebuggerDisplay("DependencyManifest: {" + nameof(ModuleName) + "}")]
    public class DependencyManifest {
        private readonly XDocument manifest;
        private List<ExpertModule> referencedModules;
        private IGlobalAttributesProvider globalAttributesProvider;

        private List<IDependencyRequirement> dependencies = new List<IDependencyRequirement>();
        private string defaultSource;

        public bool IsEnabled { get; set; }

        public bool? DependencyReplicationEnabled { get; set; }

        public DependencyManifest() {
        }

        public DependencyManifest(string moduleName, XDocument manifest) {
            this.ModuleName = moduleName;
            this.manifest = manifest;

            Initialize();
        }

        public DependencyManifest(string moduleName, Stream stream) {
            ModuleName = moduleName;
            manifest = XDocument.Load(stream, LoadOptions);

            Initialize();
        }

        private void Initialize() {
            IsEnabled = true;

            var manifestRoot = manifest.Root;

            if (manifestRoot != null) {
                bool isEnabled;
                if (manifestRoot.TryGetValueFromAttribute("IsEnabled", out isEnabled, true)) {
                    IsEnabled = isEnabled;
                }

                bool? dependencyReplicationEnabled;
                if (manifestRoot.TryGetValueFromAttribute("DependencyReplication", out dependencyReplicationEnabled, null)) {
                    DependencyReplicationEnabled = dependencyReplicationEnabled;
                }

                manifestRoot.TryGetValueFromAttribute("DefaultSource", out defaultSource, null);
            }
        }

        /// <summary>
        /// The default download source for dependencies.
        /// </summary>
        public string DefaultSource {
            get { return defaultSource; }
        }

        /// <summary>
        /// The dependency manifest file file.
        /// </summary>
        internal const string DependencyManifestFileName = "DependencyManifest.xml";

        private const LoadOptions LoadOptions = System.Xml.Linq.LoadOptions.SetBaseUri | System.Xml.Linq.LoadOptions.SetLineInfo;

        /// <summary>
        /// Gets the name of the module which this manifest is for.
        /// </summary>
        /// <value>
        /// The name of the module.
        /// </value>
        public virtual string ModuleName { get; protected set; }

        /// <summary>
        /// Gets the referenced modules.
        /// </summary>
        /// <value>
        /// The referenced modules.
        /// </value>
        /// <exception cref="Aderant.Build.DuplicateModuleInManifestException"></exception>
        public virtual IList<ExpertModule> ReferencedModules {
            get {
                if (referencedModules == null) {
                    referencedModules = new List<ExpertModule>();

                    var elements = manifest.Descendants("ReferencedModule");

                    foreach (var element in elements) {
                        XElement newElement = element;

                        if (GlobalAttributesProvider != null) {
                            var mergedElement = GlobalAttributesProvider.MergeAttributes(element);
                            if (mergedElement != null) {
                                newElement = mergedElement;
                            }
                        }

                        ExpertModule module = ExpertModule.Create(newElement);

                        if (referencedModules.Contains(module)) {
                            throw new DuplicateModuleInManifestException(string.Format(CultureInfo.InvariantCulture, "The module {0} appears more than once in {1}", module.Name, manifest.BaseUri));
                        }

                        referencedModules.Add(module);
                    }
                }
                return referencedModules;
            }
        }

        internal IGlobalAttributesProvider GlobalAttributesProvider {
            get { return globalAttributesProvider; }
            set {
                globalAttributesProvider = value;
                referencedModules = null;
            }
        }

        internal ICollection<IDependencyRequirement> Requirements {
            get { return dependencies; }
        }

        /// <summary>
        /// Loads a dependency manifest from the given module directory.
        /// </summary>
        /// <param name="modulePath">The module path.</param>
        /// <returns></returns>
        public static DependencyManifest LoadFromModule(string modulePath) {
            if (modulePath.IndexOf(Constants.BuildInfrastructureDirectory, StringComparison.OrdinalIgnoreCase) >= 0) {
                return new DependencyManifest(Constants.BuildInfrastructureDirectory, new XDocument());
            }

            return LoadDirectlyFromModule(modulePath);
        }

        /// <summary>
        /// Loads a dependency manifest from the given module directory.
        /// </summary>
        /// <param name="modulePath">The module path.</param>
        public static DependencyManifest LoadDirectlyFromModule(string modulePath) {
            DependencyManifest dependencyManifest;
            if (TryLoadFromModule(modulePath, out dependencyManifest)) {
                return dependencyManifest;
            }

            throw new InvalidOperationException("Could not load a DependencyManifest from: " + modulePath);
        }

        /// <summary>
        /// Recursively loads all dependency manifests.
        /// </summary>
        /// <param name="modulesRootPath">The modules root path.</param>
        public static IList<DependencyManifest> LoadAll(string modulesRootPath) {
            var fs = new PhysicalFileSystem(modulesRootPath);

            var directories = fs.GetDirectories(modulesRootPath);

            IList<DependencyManifest> manifests = new List<DependencyManifest>(directories.Count());

            foreach (var directory in directories) {
                DependencyManifest dependencyManifest;
                if (TryLoadFromModule(fs.GetFullPath(directory), out dependencyManifest)) {
                    manifests.Add(dependencyManifest);
                }
            }

            return manifests;
        }

        private static bool TryLoadFromModule(string modulePath, out DependencyManifest manifest) {
            try {
                manifest = LoadFromFile(modulePath);
                return true;
            } catch {
                manifest = null;
                return false;
            }
        }

        private static DependencyManifest LoadFromFile(string modulePath) {
            var fs = new PhysicalFileSystem(modulePath);
            string dependencyManifest = fs.GetFiles(modulePath, DependencyManifestFileName, true).FirstOrDefault();

            using (Stream stream = fs.OpenFile(dependencyManifest)) {
                var document = XDocument.Load(stream, LoadOptions);

                var manifest = new DependencyManifest(Path.GetFileName(modulePath), document);

                var paketFile = fs.GetFiles(modulePath, Constants.PaketDependencies, false).FirstOrDefault();
                if (!string.IsNullOrEmpty(paketFile)) {
                    manifest = new PaketView(paketFile, manifest);
                }

                return manifest;
            }
        }

        internal static DependencyManifest Parse(string moduleName, string text) {
            var manifest = new DependencyManifest(moduleName, XDocument.Parse(text, LoadOptions));

            return manifest;
        }

        internal void AddRequirement(IDependencyRequirement requirement) {
            dependencies.Add(requirement);
        }

    }
}
