using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Aderant.Build.Providers;
using Paket;

namespace Aderant.Build.DependencyAnalyzer {

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
                var dependenciesFile = DependenciesFile.ReadFromFile(paketFile);
                var dependencies = dependenciesFile.GetDependenciesInGroup(Domain.GroupName(Constants.MainDependencyGroup));

                return wrappedManifest.ReferencedModules.Union(
                    dependencies.Select(s => s.Key.Item1).Select(
                        s => new ExpertModule(s) {
                            GetAction = GetAction.NuGet,
                        })).ToList();
            }
        }
    }

    public class ExpertManifest : IModuleProvider, IGlobalAttributesProvider, IModuleGroupingSupport {
        private readonly XDocument manifest;
        private List<ExpertModule> addedModules = new List<ExpertModule>();
        private AliasTable aliasTable;

        // The dependencies manifests this instance is bound to.
        private IEnumerable<DependencyManifest> dependencyManifests;

        // The directory which contains the modules.
        private string moduleDirectory;
        
        internal ExpertManifest(XDocument manifest) {
            this.manifest = manifest;
        }

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

        public virtual string ModulesDirectory {
            get { return moduleDirectory; }
            set {
                moduleDirectory = value;
                if (string.IsNullOrEmpty(Branch)) {
                    if (Path.IsPathRooted(value)) {
                        // e.g. C:\temp\big\ -> is Git based
                        Branch = "master"; //TODO: temporary hard coded to master ???    
                    } else {
                        // TFS based, to get the TFS branch name
                        Branch = PathHelper.GetBranch(value);
                    }

                }
            }
        }

        protected IFileSystem2 FileSystem { get; }

        public XElement MergeAttributes(XElement element) {
            if (manifest.Root != null) {
                var entry = manifest.Root.Element("Modules")
                    .Descendants()
                    .FirstOrDefault(m => string.Equals(m.Attribute("Name").Value, element.Attribute("Name").Value, StringComparison.OrdinalIgnoreCase));

                if (entry != null) {
                    var mergedAttributes = MergeAttributes(entry.Attributes(), element.Attributes());
                    element.ReplaceAttributes(mergedAttributes);
                }
            }

            return element;
        }

        public void IsReplicationEnabled(DependencyManifest dependencyManifest) {
            
        }

        public bool TryGetContainer(string component, out ExpertModule container) {
            if (aliasTable == null) {
                aliasTable = new AliasTable(GetAll(), FileSystem);
            }

            return aliasTable.TryGetContainer(component, out container);

        }

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
        /// Gets the distinct complete list of available modules including those referenced in Dependency Manifests.
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<ExpertModule> GetAll() {
            IEnumerable<ExpertModule> modules = new HashSet<ExpertModule>();
            return modules;
        }

        /// <summary>
        /// Tries to get the Dependency Manifest document from the given module.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <param name="manifest">The manifest.</param>
        public virtual bool TryGetDependencyManifest(string moduleName, out DependencyManifest manifest) {manifest = null;
            return false;
        }

        /// <summary>
        ///  Determines whether the specified module is available to the current branch.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <returns>
        ///    <c>true</c> if the specified module name is available; otherwise, <c>false</c>.
        /// </returns>
        public virtual ModuleAvailability IsAvailable(string moduleName) {
            return ModuleAvailability.Availabile;
        }

        /// <summary>
        /// Gets the module with the specified name.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <returns></returns>
        public virtual ExpertModule GetModule(string moduleName) {
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
        }

        /// <summary>
        /// Loads the specified manifest.
        /// </summary>
        /// <param name="manifest">The manifest.</param>
        /// <returns></returns>
        public static ExpertManifest Load(string manifest) {
            return new ExpertManifest(XDocument.Load(manifest));
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

    internal class AliasTable : IModuleGroupingSupport {
        private readonly IFileSystem2 fileSystem;
        private readonly IEnumerable<ExpertModule> getAll;

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

}
