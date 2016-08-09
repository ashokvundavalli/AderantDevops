using System;
using System.Collections.Generic;
using System.Linq;
using Aderant.Build.DependencyAnalyzer;

namespace Aderant.Build.Providers {
    internal class DependencyManifestProvider : IModuleProvider {
        private readonly IEnumerable<ExpertModule> modules;
        private IList<DependencyManifest> manifests;
        public DependencyManifestProvider(string branchRootOrModulePath) {
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
            return manifests.SelectMany(s => s.ReferencedModules).Distinct();
        }

        public bool TryGetDependencyManifest(string moduleName, out DependencyManifest manifest) {
            manifest = manifests.FirstOrDefault(m => string.Equals(moduleName, m.ModuleName));
            if (manifest != null) {
                return true;
            }
            return false;
        }

        public bool TryGetDependencyManifestPath(string moduleName, out string manifestPath) {
            throw new NotImplementedException();
        }

        public bool IsAvailable(string moduleName) {
            throw new NotImplementedException();
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
    }
}