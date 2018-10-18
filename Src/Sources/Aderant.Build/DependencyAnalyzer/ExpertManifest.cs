using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.Process;
using Aderant.Build.Providers;
using Aderant.Build.Tasks;

namespace Aderant.Build.DependencyAnalyzer {
    public class GlobalContext {
        public string TfvcBranch { get; }
        public string TfvcChangeset { get; }

        public GlobalContext(string tfvcBranch, string tfvcChangeset) {
            TfvcBranch = tfvcBranch;
            TfvcChangeset = tfvcChangeset;
        }
    }

    public enum RepositoryType {
        Tfvc,
        Git,
    }
    internal class FileSystemModuleProvider : IModuleProvider {
        private readonly IFileSystem2 fileSystem;
        private readonly GlobalContext context;

        public FileSystemModuleProvider(IFileSystem2 fileSystem, GlobalContext context) {
            this.fileSystem = fileSystem;
            this.context = context;
        }

        public string ProductManifestPath { get; }
        public string Branch { get; }

        public IEnumerable<ExpertModule> GetAll() {
            // Ignore as we don't know the root location here.
            yield break;

        }

        public bool TryGetDependencyManifest(string moduleName, out DependencyManifest manifest) {
            manifest = null;

            string modulePath = Path.Combine(fileSystem.Root, moduleName);

            if (fileSystem.DirectoryExists(modulePath)) {
                if (fileSystem.GetFiles(modulePath, DependencyManifest.DependencyManifestFileName, true).FirstOrDefault() != null) {
                    manifest = DependencyManifest.LoadFromModule(modulePath);

                    var paketFile = fileSystem.GetFiles(modulePath, "paket.dependencies", false).FirstOrDefault();
                    if (!string.IsNullOrEmpty(paketFile)) {
                        manifest = new PaketView(fileSystem, paketFile, manifest);
                    }

                    return true;
                }
            }

            return false;
        }

        public ModuleAvailability IsAvailable(string moduleName) {
            bool exists = fileSystem.FileExists(Path.Combine(fileSystem.Root, moduleName, "Build", Constants.EntryPointFile));
            if (exists) {
                return ModuleAvailability.Availabile;
            }
            return ModuleAvailability.NotAvailabile;
        }

        public ExpertModule GetModule(string moduleName) {
            if (fileSystem.DirectoryExists(Path.Combine(fileSystem.Root, moduleName))) {
                return new ExpertModule(moduleName);
            }

            return null;
        }

        public void Add(ExpertModule module) {
            throw new NotImplementedException();
        }

        public void Remove(IEnumerable<ExpertModule> items) {
            throw new NotImplementedException();
        }

        public string Save() {
            throw new NotImplementedException();
        }

        public void GetRepositoryInfo(string moduleName) {
            throw new NotImplementedException();
        }
    }

    [DebuggerDisplay("PaketView: {wrappedManifest.ModuleName}")]
    internal class PaketView : DependencyManifest {
        private readonly IFileSystem2 fileSystem;
        private readonly string paketFile;
        private readonly DependencyManifest wrappedManifest;

        public PaketView(IFileSystem2 fileSystem, string paketFile, DependencyManifest wrappedManifest) {
            this.fileSystem = fileSystem;
            this.paketFile = paketFile;
            this.wrappedManifest = wrappedManifest;

            this.IsEnabled = true;
        }

        public override string ModuleName {
            get { return wrappedManifest.ModuleName; }
        }

        public override IList<ExpertModule> ReferencedModules {
            get {
                var dependenciesFile = Paket.DependenciesFile.ReadFromFile(paketFile);
                var dependencies = dependenciesFile.GetDependenciesInGroup(Paket.Domain.GroupName(Constants.MainDependencyGroup));

                return wrappedManifest.ReferencedModules.Union(
                    dependencies.Select(s => s.Key.Item1).Select(
                        s => new ExpertModule(s) {
                            GetAction = GetAction.NuGet,
                            RepositoryType = RepositoryType.Git // Probably
                        })).ToList();
            }
        }
    }

    internal class ManifestModuleProvider : IModuleProvider {
        private readonly IFileSystem2 fileSystem;
        private Lazy<XDocument> manifest;
        private List<ExpertModule> modules;

        public ManifestModuleProvider(IFileSystem2 fileSystem, string manifestPath) {
            this.fileSystem = fileSystem;

            manifest = new Lazy<XDocument>(() => Initialize(manifestPath));
        }

        private XDocument Initialize(string manifestPath) {
            using (Stream stream = fileSystem.OpenFile(manifestPath)) {
                var document = XDocument.Load(stream);

                ProductManifestPath = manifestPath;

                modules = LoadAllModules(document).ToList();

                Branch = PathHelper.GetBranch(ProductManifestPath ?? string.Empty, false);

                return document;
            }
        }

        private IEnumerable<ExpertModule> LoadAllModules(XDocument document) {
            IEnumerable<XElement> moduleElements = document.Root.Element("Modules").Descendants();
            return moduleElements.Select(ExpertModule.Create);
        }

        public string ProductManifestPath { get; set; }
        public string Branch { get; set; }

        public IEnumerable<ExpertModule> GetAll() {
            var document = manifest.Value;

            return modules;
        }

        public bool TryGetDependencyManifest(string moduleName, out DependencyManifest manifest) {
            //var document = this.manifest.Value;
            manifest = null;
            return false;

            //var m = modules.FirstOrDefault(m => string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase));

        }

        public ModuleAvailability IsAvailable(string moduleName) {
            return ModuleAvailability.Reference;
        }

        public ExpertModule GetModule(string moduleName) {
            var document = manifest.Value;

            return modules.FirstOrDefault(m => string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase));
        }

        public void Add(ExpertModule module) {
        }

        public void Remove(IEnumerable<ExpertModule> items) {
        }

        public string Save() {
            throw new NotImplementedException();
        }

        public void GetRepositoryInfo(string moduleName) {
            throw new NotImplementedException();
        }
    }

    public class ExpertManifest : IModuleProvider, IGlobalAttributesProvider, IModuleGroupingSupport {
        private readonly IFileSystem2 fileSystem;
        private readonly Lazy<XDocument> manifest;

        // The directory which contains the modules. Determined by the path to the manifest by convention.
        private string moduleDirectory;

        // The dependencies manifests this instance is bound to.
        private IEnumerable<DependencyManifest> dependencyManifests;

        private List<IModuleProvider> providers;
        private List<ExpertModule> addedModules = new List<ExpertModule>();
        private List<ExpertModule> removedModules = new List<ExpertModule>();
        private AliasTable aliasTable;

        /// <summary>
        /// Gets the product manifest path.
        /// </summary>
        /// <value>
        /// The product manifest path.
        /// </value>
        public virtual string ProductManifestPath { get; private set; }

        /// <summary>
        /// Gets the two part branch name
        /// </summary>
        /// <value>
        /// The branch.
        /// </value>
        public virtual string Branch { get; private set; }

        /// <summary>
        /// Gets the dependency manifests.
        /// </summary>
        /// <value>
        /// The dependency manifests.
        /// </value>
        public virtual IEnumerable<DependencyManifest> DependencyManifests {
            get {
                if (dependencyManifests == null) {
                    throw new InvalidOperationException("This instance is not bound to a specific set of dependency manifests");
                }

                return dependencyManifests;
            }
            protected set { dependencyManifests = value; }
        }

        public virtual bool HasDependencyManifests {
            get { return dependencyManifests != null; }
        }

        public virtual string ModulesDirectory {
            get { return moduleDirectory; }
            set {
                moduleDirectory = value;
                if (string.IsNullOrEmpty(Branch)) {
                    if (Path.IsPathRooted(value)) {
                        // e.g. C:\temp\big\ -> is Git based
                        Branch = "master"; //TODO: temporary hard coded to master ???    
                    } else { // TFS based, to get the TFS branch name
                        Branch = PathHelper.GetBranch(value);
                    }
                    
                }
            }
        }

        protected IFileSystem2 FileSystem {
            get { return fileSystem; }
        }

        /// <summary>
        /// Gets the distinct complete list of available modules including those referenced in Dependency Manifests.
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<ExpertModule> GetAll() {
            IEnumerable<ExpertModule> modules = new HashSet<ExpertModule>();

            foreach (var provider in providers) {
                var results = provider.GetAll();
                modules = modules.Union(results);
            }

            return modules;
        }

        /// <summary>
        /// Tries to get the Dependency Manifest document from the given module.  
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <param name="manifest">The manifest.</param>
        public virtual bool TryGetDependencyManifest(string moduleName, out DependencyManifest manifest) {
            if (dependencyManifests != null) {
                manifest = dependencyManifests.FirstOrDefault(m => string.Equals(m.ModuleName, moduleName, StringComparison.OrdinalIgnoreCase));

                if (manifest != null) {
                    return true;
                }
            } else {
                foreach (var provider in providers) {
                    if (provider.TryGetDependencyManifest(moduleName, out manifest)) {
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
        public virtual bool TryGetDependencyManifestPath(string moduleName, out string manifestPath) {
            if (moduleDirectory == null) {
                throw new ArgumentNullException(nameof(moduleDirectory), "Module directory is not specified");
            }

            string dependencyManifest = FileSystem.GetFiles(Path.Combine(FileSystem.Root, moduleName), DependencyManifest.DependencyManifestFileName, true).FirstOrDefault();

            if (dependencyManifest != null) {
                manifestPath = FileSystem.GetFullPath(dependencyManifest);
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
        public virtual ModuleAvailability IsAvailable(string moduleName) {
            ModuleAvailability curentAvailability = ModuleAvailability.NotAvailabile;
            foreach (var provider in providers) {
                var availability = provider.IsAvailable(moduleName);

                if (availability > curentAvailability) {
                    curentAvailability = availability;
                }
            }

            return curentAvailability;
        }

        /// <summary>
        /// Gets the module with the specified name.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <returns></returns>
        public virtual ExpertModule GetModule(string moduleName) {
            foreach (var provider in providers) {
                var module = provider.GetModule(moduleName);
                if (module != null) {
                    return module;
                }
            }
            return null;
        }

        public virtual void Add(ExpertModule module) {
            ExpertModule existingModule = GetModule(module.Name);

            if (existingModule == null) {
                addedModules.Add(module);
            }
        }

        /// <summary>
        /// Saves this instance to the serialized string representation.
        /// </summary>
        /// <returns></returns>
        public virtual string Save() {
            var modules = GetAll();

            var mapper = new ExpertModuleMapper();
            var result = mapper.Save(modules, true);

            return result.ToString();
        }

        /// <summary>
        /// Removes the specified items.
        /// </summary>
        /// <param name="items">The items.</param>
        public virtual void Remove(IEnumerable<ExpertModule> items) {
            foreach (var module in items) {
                removedModules.Remove(module);
            }
        }

        public ExpertManifest(IFileSystem2 fileSystem, string manifestPath)
            : this(fileSystem, manifestPath, null) {
        }

        internal ExpertManifest(XDocument manifest) {
            this.manifest = new Lazy<XDocument>(() => manifest);
        }

        public ExpertManifest(IFileSystem2 fileSystem, GlobalContext context)
            : this(fileSystem, null, context) {
        }

        private ExpertManifest(IFileSystem2 fileSystem, string manifestPath, GlobalContext context) {
            this.providers = new List<IModuleProvider>();

            if (!string.IsNullOrEmpty(manifestPath)) {
                providers.Add(new ManifestModuleProvider(fileSystem, manifestPath));
            }

            providers.Add(new FileSystemModuleProvider(fileSystem, context));

            this.fileSystem = fileSystem;
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
        /// <param name="manifestPath">The manifest.</param>
        /// <param name="dependencyManifests">The dependency manifests.</param>
        /// <returns></returns>
        public static ExpertManifest Load(string manifestPath, IEnumerable<DependencyManifest> dependencyManifests) {
            var root = new FileInfo(manifestPath).DirectoryName;
            var fs = new PhysicalFileSystem(root);
            var manifest = new ExpertManifest(fs, manifestPath);

            if (dependencyManifests != null) {
                manifest.dependencyManifests = dependencyManifests;

                foreach (DependencyManifest dependencyManifest in dependencyManifests) {
                    dependencyManifest.GlobalAttributesProvider = manifest;
                }
            }

            return manifest;
        }

        public XElement MergeAttributes(XElement element) {
            if (manifest?.Value.Root != null) {
                var entry = manifest.Value.Root.Element("Modules")
                    .Descendants()
                    .FirstOrDefault(m => string.Equals(m.Attribute("Name").Value, element.Attribute("Name").Value, StringComparison.OrdinalIgnoreCase));

                if (entry != null) {
                    var mergedAttributes = MergeAttributes(entry.Attributes(), element.Attributes());
                    element.ReplaceAttributes(mergedAttributes);
                }
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


        public bool TryGetContainer(string component, out ExpertModule container) {
            if (aliasTable == null) {
                aliasTable = new AliasTable(GetAll(), fileSystem);
            }

            return aliasTable.TryGetContainer(component, out container);

        }

        public void GetRepositoryInfo(string moduleName) {

        }
    }

    internal class AliasTable : IModuleGroupingSupport {
        private readonly IEnumerable<ExpertModule> getAll;
        private readonly IFileSystem2 fileSystem;

        private Dictionary<string, ExpertModule> aliasTable;

        public AliasTable(IEnumerable<ExpertModule> getAll, IFileSystem2 fileSystem) {
            this.getAll = getAll;
            this.fileSystem = fileSystem;
        }

        public bool TryGetContainer(string component, out ExpertModule container) {
            if (aliasTable == null) {
                aliasTable = new Dictionary<string, ExpertModule>();
                SetupAliasTable();
            }

            if (aliasTable.TryGetValue(component, out container)) {
                return true;
            }

            container = null;
            return false;
        }


        private void SetupAliasTable() {
            //  foreach (ExpertModule module in getAll) {

            IEnumerable<string> directories = fileSystem.GetDirectories(fileSystem.Root, false);

            foreach (var dir in directories) {

                foreach (string file in fileSystem.GetFiles(dir, "*.template", false)) {
                    using (Stream stream = fileSystem.OpenFile(file)) {
                        using (var reader = new StreamReader(stream)) {
                            string line;
                            while ((line = reader.ReadLine()) != null) {
                                line = line.TrimStart();

                                if (line.StartsWith("id ")) {
                                    string alias = line.Substring(3);

                                    aliasTable[alias] = new ExpertModule(Path.GetFileName(dir));
                                    break;
                                }
                            }
                        }
                    }
                }





            }
            // }
        }
    }

    internal enum ModelSource {
        File,
        FileSystem,
        InMemory,
    }
}
