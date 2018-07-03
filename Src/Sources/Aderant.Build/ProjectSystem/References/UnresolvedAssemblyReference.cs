using System.Diagnostics;

namespace Aderant.Build.ProjectSystem.References {
    [DebuggerDisplay("UnresolvedAssemblyReference: {DebuggerAssemblyName} {IsResolved}")]
    internal class UnresolvedAssemblyReference : UnresolvedReferenceBase<IUnresolvedAssemblyReference, IAssemblyReference, UnresolvedAssemblyReferenceMoniker>, IUnresolvedAssemblyReference {

        public UnresolvedAssemblyReference(AssemblyReferencesService service, UnresolvedAssemblyReferenceMoniker moniker)
            : base(service, moniker) {
        }

        public UnresolvedAssemblyReference(AssemblyReferencesServiceBase service, ConfiguredProject configuredProject)
            : base(service, configuredProject) {
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerAssemblyName {
            get { return GetAssemblyName(); }
        }

        public string GetHintPath() {
            return moniker.AssemblyPath;
        }

        public string GetAssemblyName() {
            if (IsResolved) {
                return Project.OutputAssembly;
            }

            if (moniker.AssemblyName != null) {
                return moniker.AssemblyName.FullName;
            }

            return null;
        }
    }
}
