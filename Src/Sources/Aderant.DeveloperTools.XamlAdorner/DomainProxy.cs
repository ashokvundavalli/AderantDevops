using System;
using System.Reflection;

namespace Aderant.DeveloperTools.XamlAdorner {
    internal class DomainProxy : MarshalByRefObject {

        public DomainProxy() {
           AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) {
            return null;
        }

        public Assembly GetAssembly(string assemblyPath) {
            try {
                return Assembly.LoadFile(assemblyPath);
            } catch (Exception) {
                return null;
            }
        }
    }
}
