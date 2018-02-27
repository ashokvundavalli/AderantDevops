using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Providers {
    internal class DependencyManifestProvider : IModuleProvider, IModuleGroupingSupport {
        private readonly string branchRootOrModulePath;
        private readonly IEnumerable<ExpertModule> modules;
        private IList<DependencyManifest> manifests;
        private AliasTable aliasTable;

        public DependencyManifestProvider(string branchRootOrModulePath) {
            this.branchRootOrModulePath = branchRootOrModulePath;
            manifests = DependencyManifest.LoadAll(branchRootOrModulePath);
        }

        public DependencyManifestProvider(IEnumerable<DependencyManifest> manifests) {
            this.manifests = manifests.ToList();
        }

        public DependencyManifestProvider(IEnumerable<ExpertModule> modules) {
            this.modules = modules;
        }

        public string ProductManifestPath { get; }
        public string Branch { get; }

        public IEnumerable<ExpertModule> GetAll() {
            foreach (var module in manifests.SelectMany(s => s.ReferencedModules)) {
                ExpertModule container;

                if (TryGetContainer(module.Name, out container)) {
                    yield return container;
                } else {
                    yield return module;
                }
            }
        }

        public bool TryGetDependencyManifest(string moduleName, out DependencyManifest manifest) {
            ExpertModule container;
            if (TryGetContainer(moduleName, out container)) {
                moduleName = container.Name;
            }

            manifest = manifests.FirstOrDefault(m => string.Equals(moduleName, m.ModuleName));
            if (manifest != null) {
                return true;
            }
            return false;
        }

        public bool TryGetDependencyManifestPath(string moduleName, out string manifestPath) {
            throw new NotImplementedException();
        }

        public ModuleAvailability IsAvailable(string moduleName) {
            return ModuleAvailability.Availabile;
        }

        public ExpertModule GetModule(string moduleName) {
            throw new NotImplementedException();
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

        public bool TryGetContainer(string component, out ExpertModule container) {
            if (aliasTable == null) {
                aliasTable = new AliasTable(GetAll(), new PhysicalFileSystem(branchRootOrModulePath));
            }

            return aliasTable.TryGetContainer(component, out container);

        }
    }
}