﻿using System;
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
        private readonly string paketFile;
        private readonly DependencyManifest wrappedManifest;

        public PaketView(string paketFile, DependencyManifest wrappedManifest) {
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
                    dependencies.Select(s => s.Key.Name).Select(
                        s => new ExpertModule(s) {
                            GetAction = GetAction.NuGet,
                        })).ToList();
            }
        }
    }

    public class ExpertManifest : IModuleProvider, IGlobalAttributesProvider {
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
            get; private set;
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
                if (moduleDirectory == null) {
                    throw new ArgumentNullException(nameof(moduleDirectory), "Module directory is not specified");
                }

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

        public ExpertModule GetModule(string moduleName) {
            return GetModule(moduleName, null);
        }

        /// <summary>
        /// Gets the module with the specified name.
        /// </summary>
        /// <param name="moduleName">Name of the module.</param>
        /// <param name="group">The name of the group for which the module belongs</param>
        /// <returns></returns>
        public ExpertModule GetModule(string moduleName, string group = null) {
            if (string.IsNullOrWhiteSpace(group)) {
                group = Constants.MainDependencyGroup;
            }

            foreach (ExpertModule module in modules) {
                if (string.Equals(module.Name, moduleName, StringComparison.OrdinalIgnoreCase)) {

                    if (group != null) {
                        if (string.Equals(module.DependencyGroup, group, StringComparison.OrdinalIgnoreCase)) {
                            return module;
                        }

                        if (string.Equals(group, Constants.MainDependencyGroup, StringComparison.OrdinalIgnoreCase) && module.IsInDefaultDependencyGroup) {
                            return module;
                        }
                        continue;
                    }
                    return module;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets all modules with the specified name
        /// </summary>
        /// <param name="moduleName">Name of the modules</param>
        /// <returns></returns>
        public IEnumerable<ExpertModule> GetModules(string moduleName) {
            List<ExpertModule> expertModules = modules.Where(m => string.Equals(m.Name, moduleName, StringComparison.OrdinalIgnoreCase)).ToList();
            if (expertModules.Any()) {
                return expertModules;
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

        public ExpertManifest(IFileSystem2 fileSystem, string manifestPath) {
            this.fileSystem = fileSystem;

            using (Stream stream = fileSystem.OpenFile(manifestPath)) {
                var document = XDocument.Load(stream);
                this.manifest = document;
                this.ProductManifestPath = manifestPath;
            }

            Initialize();
        }

        internal ExpertManifest(XDocument manifest) {
            this.manifest = manifest;
            Initialize();
        }

        private void Initialize() {
            this.modules = LoadAllModules().ToList();

            Branch = PathHelper.GetBranch(ProductManifestPath ?? string.Empty, false);
        }

        /// <summary>
        /// Loads the specified manifest.
        /// </summary>
        /// <param name="manifest">The manifest.</param>
        public static ExpertManifest Load(string manifest) {
            return Load(manifest, null);
        }

        /// <summary>
        /// Creates an manifest from an XML fragment
        /// </summary>
        /// <param name="productManifestXml">The product manifest XML.</param>
        public static ExpertManifest Parse(string productManifestXml) {
            return new ExpertManifest(XDocument.Parse(productManifestXml));
        }

        private IEnumerable<ExpertModule> LoadAllModules() {
            IEnumerable<XElement> moduleElements = manifest.Root.Element("Modules").Descendants();

            return moduleElements.Select(ExpertModule.Create);
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
