using System;
using System.Linq;
using System.Reflection;

namespace Aderant.Build.Utilities {
    class LoadContextAssemblyResolver : IDisposable {
        public LoadContextAssemblyResolver() {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveLoadFromAssemblies;
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private Assembly ResolveLoadFromAssemblies(object sender, ResolveEventArgs args) {
            return ResolveLoadedAssembly(args.Name);
        }

        public static Assembly ResolveLoadedAssembly(string assemblyName) {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            return assemblies.FirstOrDefault(assembly => StringComparer.OrdinalIgnoreCase.Equals((string)assembly.FullName, assemblyName));
        }

        private void Dispose(bool disposing) {
            if (disposing) {
                AppDomain.CurrentDomain.AssemblyResolve -= ResolveLoadFromAssemblies;
            }
        }
    }
}