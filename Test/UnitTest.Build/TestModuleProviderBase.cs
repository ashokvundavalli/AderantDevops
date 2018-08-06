using System.Collections.Generic;
using Aderant.Build.DependencyAnalyzer;
using Aderant.Build.Providers;

namespace UnitTest.Build {

    internal abstract class TestModuleProviderBase : IModuleProvider {

        public virtual string ProductManifestPath {
            get { throw new System.NotImplementedException(); }
        }

        public virtual string Branch {
            get { throw new System.NotImplementedException(); }
        }

        public virtual IEnumerable<ExpertModule> GetAll() {
            throw new System.NotImplementedException();
        }

        public virtual bool TryGetDependencyManifest(string moduleName, out DependencyManifest manifest) {
            throw new System.NotImplementedException();
        }

        public virtual ModuleAvailability IsAvailable(string moduleName) {
            throw new System.NotImplementedException();
        }

        public virtual ExpertModule GetModule(string moduleName) {
            throw new System.NotImplementedException();
        }

        public virtual void Add(ExpertModule module) {

        }

        public virtual void Remove(IEnumerable<ExpertModule> items) {

        }

        public virtual string Save() {
            throw new System.NotImplementedException();
        }

    }
}
