using System.Diagnostics;

namespace Aderant.Build.ProjectSystem.References {
    [DebuggerDisplay("Unresolved reference: {FullPath}")]
    internal abstract class UnresolvedReferenceBase<TUnresolvedReference, TResolvedReference>
        where TUnresolvedReference : class, IUnresolvedReference, TResolvedReference where TResolvedReference : class, IReference {

        private ResolvableReferencesProviderBase<TUnresolvedReference, TResolvedReference> provider;

        public UnresolvedReferenceBase(ResolvableReferencesProviderBase<TUnresolvedReference, TResolvedReference> provider) {
            this.provider = provider;
        }
    }
}
