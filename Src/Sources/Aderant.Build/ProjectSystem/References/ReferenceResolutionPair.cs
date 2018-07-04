using Aderant.Build.Model;

namespace Aderant.Build.ProjectSystem.References {
    public struct ReferenceResolutionPair<TUnresolvedReference, TResolvedReference>
        where TUnresolvedReference : class, TResolvedReference, IUnresolvedReference
        where TResolvedReference : class, IReference {

        public ReferenceResolutionPair(TUnresolvedReference existingUnresolvedItem, TResolvedReference resolvedReference) {
            this.ExistingUnresolvedItem = existingUnresolvedItem;
            this.ResolvedReference = resolvedReference;
        }

        /// <summary>
        /// Gets the existing unresolved item.
        /// </summary>
        public TUnresolvedReference ExistingUnresolvedItem { get; private set; }

        /// <summary>
        /// Gets the resolved reference.
        /// </summary>
        public TResolvedReference ResolvedReference { get; private set; }
    }
}
