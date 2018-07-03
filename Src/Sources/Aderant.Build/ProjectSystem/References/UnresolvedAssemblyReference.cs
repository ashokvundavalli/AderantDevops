using System.Diagnostics;

namespace Aderant.Build.ProjectSystem.References {
    [DebuggerDisplay("UnresolvedAssemblyReference: {AssemblyName}")]
    internal class UnresolvedAssemblyReference : UnresolvedReferenceBase<IUnresolvedAssemblyReference, IAssemblyReference>,
        IUnresolvedAssemblyReference {

        private readonly AssemblyReferencesServiceBase provider;
        private UnresolvedAssemblyReferenceMoniker moniker;

        public UnresolvedAssemblyReference(AssemblyReferencesServiceBase provider)
            : base(provider) {
            this.provider = provider;
        }

        public string GetHintPath() {
            return moniker.AssemblyPath;
        }

        public void Initialize(UnresolvedAssemblyReferenceMoniker moniker) {
            this.moniker = moniker;
        }
    }
}
