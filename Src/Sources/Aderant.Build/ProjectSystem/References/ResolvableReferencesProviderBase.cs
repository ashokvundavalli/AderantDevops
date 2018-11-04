using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Aderant.Build.Model;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem.References {

    internal abstract class ResolvableReferencesProviderBase<TUnresolvedReference, TResolvedReference> : IResolvableReferencesService<TUnresolvedReference, TResolvedReference>
        where TUnresolvedReference : class, IUnresolvedReference, TResolvedReference where TResolvedReference : class, IReference {

        private readonly string unresolvedReferenceType;

        protected ResolvableReferencesProviderBase(string unresolvedReferenceType) {
            this.unresolvedReferenceType = unresolvedReferenceType;
        }

        /// <summary>
        /// Gets the configured project this service is bound to.
        /// </summary>
        [Import]
        protected internal ConfiguredProject ConfiguredProject { get; private set; }
        
        public List<TUnresolvedReference> UnresolvedReferences { get; set; }

        public virtual IReadOnlyCollection<TUnresolvedReference> GetUnresolvedReferences() {
            ICollection<ProjectItem> projectItems = ConfiguredProject.GetItems(unresolvedReferenceType);

            List<TUnresolvedReference> references = new List<TUnresolvedReference>();

            foreach (var projectItem in projectItems) {
                var unresolvedReference = CreateUnresolvedReference(projectItem);
                references.Add(unresolvedReference);
            }

            return UnresolvedReferences = references;
        }

        public IReadOnlyCollection<ResolvedDependency<TUnresolvedReference, TResolvedReference>> GetResolvedReferences(IReadOnlyCollection<IUnresolvedReference> references) {
            if (UnresolvedReferences == null) {
                throw new InvalidOperationException("GetUnresolvedReferences not called");
            }

            var nowResolvedReferences = new List<ResolvedDependency<TUnresolvedReference, TResolvedReference>>();

            Resolve(UnresolvedReferences, references, nowResolvedReferences);

            foreach (var nowResolvedReference in nowResolvedReferences) {
                UnresolvedReferences.Remove(nowResolvedReference.ExistingUnresolvedItem);
            }

            return nowResolvedReferences;
        }

        private void Resolve(IEnumerable<TUnresolvedReference> list, IReadOnlyCollection<IUnresolvedReference> references, List<ResolvedDependency<TUnresolvedReference, TResolvedReference>> nowResolvedReferences) {
            foreach (var unresolved in list) {
                TResolvedReference resolvedReference = CreateResolvedReference(references, unresolved);
                if (resolvedReference != null) {
                    nowResolvedReferences.Add(new ResolvedDependency<TUnresolvedReference, TResolvedReference>(ConfiguredProject, resolvedReference, unresolved));
                }
            }
        }

        protected abstract TResolvedReference CreateResolvedReference(IReadOnlyCollection<IUnresolvedReference> references, TUnresolvedReference unresolved);

        protected abstract TUnresolvedReference CreateUnresolvedReference(ProjectItem unresolved);
    }
}
