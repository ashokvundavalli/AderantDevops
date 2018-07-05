using Aderant.Build.Model;

namespace Aderant.Build.ProjectSystem.References {

    public class ResolvedReference : ResolvedDependency<IUnresolvedReference, IReference> {

        public ResolvedReference(IArtifact artifact, IUnresolvedReference existingUnresolvedItem, IReference resolvedReference)
            : base(artifact, null, existingUnresolvedItem) {
            ExistingUnresolvedItem = existingUnresolvedItem;
            ResolvedReference = resolvedReference;
        }

        public virtual void ReplaceReference(IDependable item) {
            var newReference = item as IReference;

            if (newReference != null) {
                ResolvedReference = newReference;
            }
        }
    }
}
