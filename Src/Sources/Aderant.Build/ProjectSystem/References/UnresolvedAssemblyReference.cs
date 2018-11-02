using System.Diagnostics;

namespace Aderant.Build.ProjectSystem.References {
    [DebuggerDisplay("UnresolvedAssemblyReference: {Id}")]
    internal class UnresolvedAssemblyReference : UnresolvedReferenceBase<IUnresolvedAssemblyReference, IAssemblyReference, UnresolvedAssemblyReferenceMoniker>, IUnresolvedAssemblyReference {

        public UnresolvedAssemblyReference(AssemblyReferencesService service, UnresolvedAssemblyReferenceMoniker moniker)
            : base(service, moniker) {
        }

        public string GetHintPath() {
            return moniker.AssemblyPath;
        }

        public string GetAssemblyName() {
            return moniker.AssemblyName.Name;
        }

        public override string Id {
            get { return GetAssemblyName(); }
        }
    }
}
