namespace Aderant.Build.ProjectSystem.References {
    internal abstract class UnresolvedReferenceBase<TUnresolvedReference, TResolvedReference, TMoniker>
        where TUnresolvedReference : class, IUnresolvedReference, TResolvedReference where TResolvedReference : class, IReference {

        protected readonly TMoniker moniker;

        private ConfiguredProject project;

        private ResolvableReferencesProviderBase<TUnresolvedReference, TResolvedReference> service;

        protected UnresolvedReferenceBase(ResolvableReferencesProviderBase<TUnresolvedReference, TResolvedReference> service, TMoniker moniker) {
            this.service = service;
            this.moniker = moniker;
        }

        protected UnresolvedReferenceBase(ResolvableReferencesProviderBase<TUnresolvedReference, TResolvedReference> service, ConfiguredProject configuredProject) {
            this.service = service;
            this.Project = configuredProject;
        }

        public bool IsResolved { get; set; }

        protected ConfiguredProject Project {
            get { return project; }
            set {
                project = value;
                IsResolved = true;
            }
        }
    }
}
