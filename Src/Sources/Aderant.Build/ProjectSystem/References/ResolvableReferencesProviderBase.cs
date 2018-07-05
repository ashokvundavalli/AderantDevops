using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Aderant.Build.Model;
using Microsoft.Build.Evaluation;

namespace Aderant.Build.ProjectSystem.References {

    internal abstract class ResolvableReferencesProviderBase<TUnresolvedReference, TResolvedReference> : IResolvableReferencesService<TUnresolvedReference, TResolvedReference>, IDisposable
        where TUnresolvedReference : class, IUnresolvedReference, TResolvedReference where TResolvedReference : class, IReference {

        private readonly string unresolvedReferenceType;
        private List<TUnresolvedReference> unresolvedReferences;

        protected ResolvableReferencesProviderBase(string unresolvedReferenceType) {
            this.unresolvedReferenceType = unresolvedReferenceType;
        }

        /// <summary>
        /// Gets the configured project this service is bound to.
        /// </summary>
        [Import]
        protected internal ConfiguredProject ConfiguredProject { get; private set; }

        public void Dispose() {
            this.Dispose(true);
        }

        public virtual IReadOnlyCollection<TUnresolvedReference> GetUnresolvedReferences() {
            ICollection<ProjectItem> projectItems = ConfiguredProject.GetItems(unresolvedReferenceType);

            List<TUnresolvedReference> references = new List<TUnresolvedReference>();

            foreach (var projectItem in projectItems) {
                var unresolvedReference = CreateUnresolvedReference(projectItem);
                references.Add(unresolvedReference);
            }

            return unresolvedReferences = references;
        }

        public IReadOnlyCollection<ResolvedDependency<TUnresolvedReference, TResolvedReference>> GetResolvedReferences(IReadOnlyCollection<IUnresolvedReference> references) {
            var nowResolvedReferences = new List<ResolvedDependency<TUnresolvedReference, TResolvedReference>>();

            foreach (var unresolved in unresolvedReferences) {
                TResolvedReference resolvedReference = CreateResolvedReference(references, unresolved);
                if (resolvedReference != null) {
                    nowResolvedReferences.Add(new ResolvedDependency<TUnresolvedReference, TResolvedReference>(ConfiguredProject, resolvedReference, unresolved));
                }
            }

            foreach (var nowResolvedReference in nowResolvedReferences) {
                unresolvedReferences.Remove(nowResolvedReference.ExistingUnresolvedItem);
            }

            return nowResolvedReferences;
        }

        protected abstract TResolvedReference CreateResolvedReference(IReadOnlyCollection<IUnresolvedReference> references, TUnresolvedReference unresolved);

        protected abstract TUnresolvedReference CreateUnresolvedReference(ProjectItem unresolved);

        protected virtual void Dispose(bool disposing) {
        }
    }
}
