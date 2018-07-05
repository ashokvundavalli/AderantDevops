using System.Diagnostics;
using Aderant.Build.Model;

namespace Aderant.Build.ProjectSystem.References {
    [DebuggerDisplay("UnresolvedAssemblyReference: {Id}")]
    internal class UnresolvedAssemblyReference : UnresolvedReferenceBase<IUnresolvedAssemblyReference, IAssemblyReference, UnresolvedAssemblyReferenceMoniker>, IUnresolvedAssemblyReference {

        public UnresolvedAssemblyReference(AssemblyReferencesService service, UnresolvedAssemblyReferenceMoniker moniker)
            : base(service, moniker) {
        }

        public UnresolvedAssemblyReference(AssemblyReferencesService service, IUnresolvedAssemblyReference unresolved, IReference resolvedReference)
            : base(service, unresolved, resolvedReference) {
        }

        public string GetHintPath() {
            return moniker.AssemblyPath;
        }

        public string GetAssemblyName() {
            if (IsResolved) {
                return ResolvedReference.GetAssemblyName();
            }

            return moniker.AssemblyName.FullName;
        }

        public override string Id {
            get { return GetAssemblyName(); }
        }
    }
}
