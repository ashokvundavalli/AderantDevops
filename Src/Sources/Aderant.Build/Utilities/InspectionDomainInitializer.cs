using System;
using System.Reflection;

namespace Aderant.Build.Utilities {
    class InspectionDomainInitializer : MarshalByRefObject, IDisposable {
        private LoadContextAssemblyResolver assemblyResolver = new LoadContextAssemblyResolver();

        public InspectionDomainInitializer() {
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing) {
            if (disposing) {
                assemblyResolver.Dispose();
            }
        }

        public void LoadAssembly(string assemblyLocation) {
            Assembly.LoadFrom(assemblyLocation);
        }
    }
}