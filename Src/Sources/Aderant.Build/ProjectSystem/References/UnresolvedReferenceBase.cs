using Aderant.Build.Model;

namespace Aderant.Build.ProjectSystem.References {

    /// <summary>
    /// UnresolvedReference objects are used to represent dependencies between artifacts when the actual artifacts are not
    /// known yet.
    /// During the analysis process performed by the engine, UnresolvedReference objects are replaced by links to actual
    /// artifact
    /// objects.
    /// </summary>
    internal abstract class UnresolvedReferenceBase<TUnresolvedReference, TResolvedReference, TMoniker> : IDependable
        where TUnresolvedReference : class, IUnresolvedReference, TResolvedReference where TResolvedReference : class, IReference {

        protected readonly TMoniker moniker;

        private ResolvableReferencesProviderBase<TUnresolvedReference, TResolvedReference> service;

        protected UnresolvedReferenceBase(ResolvableReferencesProviderBase<TUnresolvedReference, TResolvedReference> service, TMoniker moniker) {
            this.service = service;
            this.moniker = moniker;
        }

        /// <summary>
        /// Gets the unique moniker that identifies this dependency within the build.
        /// </summary>
        public abstract string Id { get; }
    }
}
